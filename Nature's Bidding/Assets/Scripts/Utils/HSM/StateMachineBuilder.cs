using System.Collections.Generic;
using System.Reflection;

namespace HSM
{
    internal class StateMachineBuilder
    {
        readonly State root;

        public StateMachineBuilder(State root)
        {
            this.root = root;
        }

        public StateMachine Build()
        {
            var m = new StateMachine(root);
            Wire(root, m, new HashSet<State>());

            return m;
        }

        /// <summary>
        /// Recurses through tree to "wire up" states on build by injecting a reference to the state machine.
        /// </summary>
        /// <param name="s"></param>
        /// <param name="m"></param>
        /// <param name="visited"></param>
        void Wire(State s, StateMachine m, HashSet<State> visited)
        {
            if (s == null) return;
            if (!visited.Add(s)) return;

            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;
            var machineField = typeof(State).GetField("Machine", flags);

            // Set machine reference on build
            if (machineField != null) machineField.SetValue(s, m);

            foreach (var fld in s.GetType().GetFields(flags))
            {
                // Get child field
                if (!typeof(State).IsAssignableFrom(fld.FieldType)) continue;
                if (fld.Name == "Parent") continue;

                // Get valid child from field or continue
                var child = (State)fld.GetValue(s);
                if (child == null) continue;
                if (!ReferenceEquals(child.Parent, s)) continue;

                // Recurse into children
                Wire(child, m, visited);
            }
        }
    }
}
