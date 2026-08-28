using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace HSM
{
    public class TransitionSequencer
    {
        public bool IsPhaseActive => sequencer != null;
        public float PhaseTimerDebug => _phaseTimer;

        public readonly StateMachine Machine;
        public readonly bool UseSequential = true;

        ISequence sequencer;
        Action nextPhase;
        (State from, State to)? pending;

        CancellationTokenSource cts = new CancellationTokenSource();

        float _phaseTimer;
        const float MaxPhaseSeconds = 2f;

        public TransitionSequencer(StateMachine machine)
        {
            Machine = machine;
        }

        public void RequestTransition(State from, State to)
        {
            if (to == null || from == to) return;

            if (sequencer != null)
            {
                // Queue at most one pending transition; newest wins
                pending = (from, to);
                return;
            }

            BeginTransition(from, to);
        }

        void BeginTransition(State from, State to)
        {
            cts?.Cancel();
            cts = new CancellationTokenSource();

            State lca = LCA(from, to);
            var exitChain = StatesToExit(from, lca);

            var exitSteps = GatherPhaseSteps(exitChain, deactivate: true);

            sequencer = UseSequential
                ? new SequentialPhase(exitSteps, cts.Token)
                : new ParallelPhase(exitSteps, cts.Token);

            sequencer.Start();

            nextPhase = () =>
            {
                Machine.ChangeState(from, to);

                // Resolve late to allow OnExit logic to affect initial state decision
                State leaf = to.ResolveLeaf();
                var enterChain = StatesToEnter(leaf, lca);
                var enterSteps = GatherPhaseSteps(enterChain, deactivate: false);

                sequencer = UseSequential
                    ? new SequentialPhase(enterSteps, cts.Token)
                    : new ParallelPhase(enterSteps, cts.Token);

                sequencer.Start();
            };
        }

        void EndTransition()
        {
            sequencer = null;
            _phaseTimer = 0f;

            if (pending.HasValue)
            {
                var p = pending.Value;
                pending = null;

                State currentLeaf = Machine.Root.Leaf();
                if (p.to != null && p.to != currentLeaf)
                    BeginTransition(currentLeaf, p.to);
            }
        }

        public void Tick(float deltaTime)
        {
            if (sequencer != null)
            {
                bool phaseDone;
                try
                {
                    phaseDone = sequencer.Update();
                }
                catch (Exception e)
                {
                    GameLogger.LogException(LogSeverity.Error, "[HSM] Phase Update threw — force-completing transition.", e);
                    phaseDone = true;
                }

                if (phaseDone)
                {
                    _phaseTimer = 0f;
                    if (nextPhase != null)
                    {
                        var n = nextPhase;
                        nextPhase = null;
                        try { n(); }
                        catch (Exception e)
                        {
                            GameLogger.LogException(LogSeverity.Error, "[HSM] nextPhase threw — abandoning transition.", e);
                            sequencer = null;
                            EndTransition();
                        }
                    }
                    else
                    {
                        EndTransition();
                    }
                }
                else // Ensure no phase is wedged indefinitely; if it exceeds MaxPhaseSeconds, cancel and force nextPhase.
                {
                    _phaseTimer += deltaTime;
                    if (_phaseTimer > MaxPhaseSeconds)
                    {
                        GameLogger.Log(LogSeverity.Error, $"[HSM] Phase exceeded {MaxPhaseSeconds}s — force-cancelling wedged transition.");
                        cts?.Cancel();
                        _phaseTimer = 0f;
                        var n = nextPhase;
                        nextPhase = null;
                        sequencer = null;
                        if (n != null) { try { n(); } catch (Exception e) { GameLogger.LogException(LogSeverity.Error, "[HSM] Forced nextPhase threw.", e); sequencer = null; } }
                        if (sequencer == null) EndTransition();
                    }
                }
                return;
            }

            Machine.InternalTick(deltaTime);
        }

        // ─── Static path helpers ──────────────────────────────────────────────

        public static State LCA(State a, State b)
        {
            var aParents = new HashSet<State>();
            for (State s = a; s != null; s = s.Parent) aParents.Add(s);
            for (State s = b; s != null; s = s.Parent)
                if (aParents.Contains(s)) return s;
            return null;
        }

        public static List<State> StatesToExit(State from, State lca)
        {
            var states = new List<State>();
            for (var s = from; s != null && s != lca; s = s.Parent) states.Add(s);
            return states;
        }

        public static List<State> StatesToEnter(State leaf, State lca)
        {
            var stack = new Stack<State>();
            for (var s = leaf; s != null && s != lca; s = s.Parent) stack.Push(s);
            return new List<State>(stack);
        }

        static List<PhaseStep> GatherPhaseSteps(List<State> chain, bool deactivate)
        {
            var steps = new List<PhaseStep>();
            for (int i = 0; i < chain.Count; i++)
            {
                var acts = chain[i].Activities;
                for (int j = 0; j < acts.Count; j++)
                {
                    var a = acts[j];
                    bool include = deactivate
                        ? a.Mode == ActivityMode.Active
                        : a.Mode == ActivityMode.Inactive;
                    if (!include) continue;
                    steps.Add(ct => deactivate ? a.DeactivateAsync(ct) : a.ActivateAsync(ct));
                }
            }
            return steps;
        }
    }
}
