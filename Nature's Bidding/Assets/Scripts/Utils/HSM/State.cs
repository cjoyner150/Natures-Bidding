using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace HSM
{
    public abstract class State {
        public readonly StateMachine Machine;
        public readonly State Parent;
        public State ActiveChild;

        readonly List<IActivity> activities = new List<IActivity>();
        public IReadOnlyList<IActivity> Activities => activities;

        public State(StateMachine machine, State parent = null)
        {
            Machine = machine;
            Parent = parent;
        }

        public void Add(IActivity a) { if (a != null)  activities.Add(a); }

        /// <summary>
        /// Get the initial child state that should be entered when this state begins (null = this is the leaf)
        /// </summary>
        /// <returns></returns>
        protected virtual State GetInitialState() => null;

        /// <summary>
        /// Target state to switch to this frame (null = stay in this state this frame)
        /// </summary>
        /// <returns></returns>
        protected virtual State GetTransition() => null;

        #region Overridable lifecycle methods

        protected virtual void OnEnter() { }
        protected virtual void OnExit() { }
        protected virtual void OnUpdate(float deltaTime) { }

        #endregion

        #region Invariable sequence methods

        /// <summary>
        /// Invariable enter method. Behavior should be implemented in overriden OnEnter().
        /// </summary>
        internal void Enter() {
            //Debug.Log($"{this.GetType().Name} is being entered");

            if (Parent != null) Parent.ActiveChild = this;
            
            OnEnter();

            // If there is an initial child state defined, enter it
            State init = GetInitialState();
            if (init != null) init.Enter();
        }

        /// <summary>
        /// Invariable exit method. Behavior should be implemented in overriden OnExit().
        /// </summary>
        internal void Exit()
        {
            if (ActiveChild != null) ActiveChild.Exit();
            ActiveChild = null;

            OnExit();
        }

        /// <summary>
        /// Invariable update method. Behavior should be implemented in overriden OnUpdate().
        /// </summary>
        internal void Update(float deltaTime) {

            State t = GetTransition(); 
            if (t != null) // If we find a transition this frame, request a transition and return out of update
            {
                Machine.Sequencer.RequestTransition(Leaf(), t);
                return;
            }

            if (ActiveChild != null) ActiveChild.Update(deltaTime);

            OnUpdate(deltaTime);
        }

        #endregion

        #region Helper methods

        /// <summary>
        /// Returns deepest currently active state. (The leaf of the active path)
        /// </summary>
        /// <returns></returns>
        public State Leaf()
        {
            State s = this;
            while (s.ActiveChild != null) s = s.ActiveChild;

            return s;
        }

        /// <summary>
        /// Yields this state and then each state up the path. (self -> parent -> ... -> root)
        /// </summary>
        /// <returns></returns>
        public IEnumerable<State> GetActivePath()
        {
            for (State s = this; s != null; s = s.Parent) yield return s;
        }

        public T GetParentOfType<T>() where T : State
        {
            State p = this;

            while (p != null)
            {
                if (p.IsConvertibleTo<T>(true))
                {
                    return (T)p;
                }

                p = p.Parent;

            }

            return null;
        }

        #endregion

    }

}
