using MoreMountains.Tools;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace HSM
{
    public abstract class State
    {
        public readonly StateMachine Machine;
        public readonly State Parent;
        public State ActiveChild { get; internal set; }

        readonly List<IActivity> activities = new List<IActivity>();
        public IReadOnlyList<IActivity> Activities => activities;

        public State(StateMachine machine, State parent = null)
        {
            Machine = machine;
            Parent = parent;
        }

        public void Add(IActivity a) { if (a != null) activities.Add(a); }

        // ─── Overridable lifecycle ────────────────────────────────────────────

        /// <summary>
        /// Return the child state that should be active when this state is
        /// entered with no explicit target. Returning null means this is a leaf.
        /// </summary>
        protected virtual State GetInitialState() => null;

        /// <summary>
        /// Return a non-null state to request a transition this tick.
        /// </summary>
        protected virtual State GetTransition() => null;

        protected virtual void OnEnter() { }
        protected virtual void OnExit() { }
        protected virtual void OnUpdate(float deltaTime) { }

        // ─── Internal lifecycle (called by StateMachine / TransitionSequencer) ─

        /// <summary>
        /// Pure lifecycle enter. No child cascading — path resolution is the
        /// sequencer's responsibility.
        /// </summary>
        internal void Enter()
        {
            GameLogger.Log(LogSeverity.Debug, $"[Enter] {GetType().Name}");
            if (Parent != null) Parent.ActiveChild = this;
            OnEnter();
        }

        /// <summary>
        /// Recursive exit — exits active child first, then self.
        /// </summary>
        internal void Exit()
        {
            GameLogger.Log(LogSeverity.Debug, $"[Exit] {GetType().Name}");
            OnExit();
        }

        internal void Update(float deltaTime)
        {
            State t = GetTransition();
            if (t != null && t != Leaf())          // suppress only true no-ops: requesting the already-active leaf
            {
                Machine.Sequencer.RequestTransition(Leaf(), t);
                return;
            }

            if (ActiveChild != null) ActiveChild.Update(deltaTime);
            OnUpdate(deltaTime);
        }

        // ─── Internal path resolution ─────────────────────────────────────────

        /// <summary>
        /// Walks GetInitialState() from this state down to the leaf that would
        /// be entered if this state were targeted in a transition.
        /// Includes cycle detection.
        /// </summary>
        internal State ResolveLeaf()
        {
            var visited = new HashSet<State>();
            State s = this;
            while (true)
            {
                if (!visited.Add(s))
                {
                    GameLogger.Log(LogSeverity.Error, $"[HSM] Cycle detected in GetInitialState() at {s.GetType().Name}. Stopping resolution.");
                    return s;
                }
                State next = s.GetInitialState();
                if (next == null) return s;
                s = next;
            }
        }

        // ─── Public helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Returns the deepest currently active state.
        /// </summary>
        public State Leaf()
        {
            State s = this;
            while (s.ActiveChild != null) s = s.ActiveChild;
            return s;
        }

        /// <summary>
        /// Walks up the parent chain and returns the first ancestor of type T.
        /// </summary>
        public T GetParentOfType<T>() where T : State
        {
            for (State p = Parent; p != null; p = p.Parent)
                if (p is T t) return t;
            return null;
        }

        /// <summary>
        /// Yields self and each ancestor up to the root.
        /// </summary>
        public IEnumerable<State> AncestorPath()
        {
            for (State s = this; s != null; s = s.Parent) yield return s;
        }
    }
}
