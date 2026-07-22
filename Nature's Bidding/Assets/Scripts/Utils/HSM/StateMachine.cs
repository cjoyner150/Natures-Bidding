using MoreMountains.Tools;
using System.Collections.Generic;
using UnityEngine;

namespace HSM
{
    public class StateMachine
    {
        public readonly State Root;
        public readonly TransitionSequencer Sequencer;

        bool started = false;

        public StateMachine(State root)
        {
            Root = root;
            Sequencer = new TransitionSequencer(this);
        }

        public void Start()
        {
            if (started) return;
            started = true;
            // Resolve the full initial path from root to leaf and enter each state
            EnterChain(Root, Root.ResolveLeaf(), parent: null);
        }

        public void Tick(float deltaTime)
        {
            if (!started) Start();
            Sequencer.Tick(deltaTime);
        }

        internal void InternalTick(float deltaTime) => Root.Update(deltaTime);

        /// <summary>
        /// Called by TransitionSequencer once exit activities have completed.
        /// Performs the atomic state change: exits from->lca, enters lca->leaf(to).
        /// </summary>
        internal void ChangeState(State from, State to)
        {
            HSMDebug.Log($"[ChangeState] {from.GetType().Name} → {to.GetType().Name}");

            if (from == to || from == null || to == null) return;

            State lca = TransitionSequencer.LCA(from, to);
            State leaf = to.ResolveLeaf();

            // 'from' may be an ancestor that requested the transition (e.g. Grounded → Airborne
            // while the active leaf is Attack). Descend to the deepest active state first so
            // every nested OnExit runs.
            State exitLeaf = from;
            while (exitLeaf.ActiveChild != null) exitLeaf = exitLeaf.ActiveChild;

            // Exit leaf → lca (exclusive), leaf-first ordering, each state exactly once.
            for (State s = exitLeaf; s != lca; s = s.Parent)
            {
                s.Exit();
                if (s.Parent != null) s.Parent.ActiveChild = null;
            }

            EnterChain(lca, leaf, parent: lca);
        }

        /// <summary>
        /// Enters every state on the path from (exclusive) down to leaf (inclusive).
        /// </summary>
        static void EnterChain(State ancestor, State leaf, State parent)
        {
            // Build the ordered path from just-below-ancestor down to leaf
            var stack = new Stack<State>();
            for (State s = leaf; s != ancestor; s = s.Parent)
            {
                if (s == null)
                {
                    Debug.LogError("[HSM] EnterChain: leaf is not a descendant of ancestor. Aborting.");
                    return;
                }
                stack.Push(s);
            }
            while (stack.Count > 0) stack.Pop().Enter();
        }
    }

    public static class HSMDebug
    {
        public static bool Enabled = false;

        public static void Log(string msg)
        {
#if UNITY_EDITOR
            if (Enabled) UnityEngine.Debug.Log($"[HSM] {msg}");
#endif
        }
    }
}
