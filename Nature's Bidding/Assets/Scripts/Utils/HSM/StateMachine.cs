using System.Collections.Generic;
using UnityEngine;

namespace HSM
{
    public class StateMachine {
        public readonly State Root;
        public readonly TransitionSequencer Sequencer;
        bool started = false;

        public StateMachine(State Root)
        {
            this.Root = Root;
            Sequencer = new TransitionSequencer(this);
        }

        public void Start()
        {
            if (started) return;

            started = true;
            Root.Enter();
        }

        public void Tick(float deltaTime)
        {
            if (!started) Start();

            Sequencer.Tick(deltaTime);
        }

        internal void InternalTick(float deltaTime) => Root.Update(deltaTime);

        /// <summary>
        /// Change state called from Transition Sequencer
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        public void ChangeState(State from, State to)
        {
            Debug.Log($"Attempting to change from {from.GetType().Name} to {to.GetType().Name}");

            if (from == to || from == null || to == null) return;

            State lca = TransitionSequencer.LCA(from, to);

            // Exit upwards from the from state until the lca but do not exit the lca
            for (State s = from; s != lca; s = s.Parent) s.Exit();

            // Enter down the tree from the lca to the target
            Stack<State> stack = new Stack<State>();
            for (State s = to; s != lca; s = s.Parent) stack.Push(s);
            while (stack.Count > 0) stack.Pop().Enter();

        }
    }
}
