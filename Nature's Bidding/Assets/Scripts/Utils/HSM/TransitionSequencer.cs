using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace HSM
{
    public class TransitionSequencer {
        public readonly StateMachine Machine;

        ISequence sequencer;
        Action nextPhase;
        (State from, State to)? pending;
        State lastFrom, lastTo;

        public TransitionSequencer(StateMachine machine) { 
            Machine = machine;
        }

        public void RequestTransition(State from, State to) {

            if (to == null || from == to) return;
            if (sequencer != null) { pending = (from, to); return; }

            BeginTransition(from, to);
        }

        static List<PhaseStep> GatherPhaseSteps(List<State> chain, bool deactivate)
        {
            var steps = new List<PhaseStep>();

            for (int i = 0; i < chain.Count; i++)
            {
                var st = chain[i];
                var acts = chain[i].Activities;
                for (int j = 0; j < acts.Count; j++)
                {
                    var a = acts[j];
                    bool include = deactivate ? (a.Mode == ActivityMode.Active)
                        : (a.Mode == ActivityMode.Inactive);
                    if (!include) continue;

                    Debug.Log($"[Phase {(deactivate ? "Exit" : "Enter")}] state={st.GetType().Name}, activity={a.GetType().Name}, mode={a.Mode}");

                    steps.Add(ct => deactivate ? a.DeactivateAsync(ct) : a.ActivateAsync(ct));
                }
            }
            return steps;
        }

        public static List<State> StatesToExit(State from, State lca)
        {
            List<State> states = new List<State>();
            for (var s = from; s!= null & s != lca; s = s.Parent) states.Add(s); 
            return states;
        }

        public static List<State> StatesToEnter(State to, State lca)
        {
            Stack<State> statesStack = new Stack<State>();

            for (var s = to; s != null && s != lca; s = s.Parent) statesStack.Push(s);

            return new List<State>(statesStack);
        }

        CancellationTokenSource cts = new CancellationTokenSource();
        public readonly bool UseSequential = true;

        void BeginTransition(State from, State to) {
            cts?.Cancel();
            cts = new CancellationTokenSource();

            var lca = LCA(from, to);
            var exitChain = StatesToExit(from, lca);
            var enterChain = StatesToEnter(to, lca);

            var exitSteps = GatherPhaseSteps(exitChain, deactivate: true);

            sequencer = UseSequential 
                ? new SequentialPhase(exitSteps, cts.Token) 
                : new ParallelPhase(exitSteps, cts.Token);

            sequencer.Start();

            nextPhase = () =>
            {
                Machine.ChangeState(from, to);

                var enterSteps = GatherPhaseSteps(enterChain, deactivate: false);

                sequencer = UseSequential
                    ? new SequentialPhase(enterSteps, cts.Token)
                    : new ParallelPhase(enterSteps, cts.Token);

                sequencer.Start();
            };
        }

        void EndTransition() {
            sequencer = null;

            if (pending.HasValue)
            {
                var p = pending.Value;
                pending = null;

                BeginTransition(p.from, p.to);
            }
        }

        public void Tick(float deltaTime)
        {
            if (sequencer != null)
            {
                if (sequencer.Update())
                {
                    if (nextPhase != null)
                    {
                        var n = nextPhase;
                        nextPhase = null;
                        n();
                    }
                    else
                    {
                        EndTransition();
                    }
                }
                return;
            }

            Machine.InternalTick(deltaTime);
        }


        /// <summary>
        /// Returns the lowest common ancestor of two states.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static State LCA(State a, State b)
        {
            HashSet<State> aParents = new HashSet<State>();

            // Add all of state a's parents to a hashset
            for (State s = a; s != null; s = s.Parent) aParents.Add(s);

            // Iterate upwards from state b until a common ancestor is found
            for (State s = b; s != null; s = s.Parent) 
            { 
                if (aParents.Contains(s)) return s; 
            }

            // No common ancestor found
            return null;

        }

    }
}
