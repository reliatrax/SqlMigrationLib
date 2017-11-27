using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SqlMigrationLib
{
    /*
    Paste code below into http://www.webgraphviz.com/ for a diagram:

    digraph SqlCommentStripper
    {
     A  -> M1 [ label="'/'"];
     A  -> S1 [ label="'-'"];
     A  -> A  [ label=" default" ];
     M1 -> M2 [ label="'*'"];
     M1 -> A  [ label=" default"];
     M2 -> M3 [ label="'*'"];
     M2 -> M2 [ label=" default"];
     M3 -> A  [ label="'/'"];
     M3 -> M3 [ label="'*'"];
     M3 -> M2 [ label=" default"];
     S1 -> S2 [ label="'-'"];
     S1 -> A  [ label=" default"];
     S2 -> A  [ label="'\\n'"];
     S2 -> S2 [ label=" default"];
    }
    */
    public static class CommentStripper
    {
        enum State { A, M1, M2, M3, S1, S2 };

        struct Edge
        {
            public char C;
            public State Next;
            public Action<StringBuilder,char> Action;

            public Edge(char ch, State next, Action<StringBuilder,char> action)
            {
                C = ch;
                Next = next;
                Action = action;
            }
        }

        static Dictionary<State, Edge[]> transitions = new Dictionary<State, Edge[]> {
            { State.A,  new Edge[] {
                new Edge( '/', State.M1, null),
                new Edge('-', State.S1, null),
                new Edge('\0', State.A, (sb,c) => sb.Append(c))     // the standard case, we're not in a comment so just output the character
                } },
            { State.M1, new Edge[] {
                new Edge( '*', State.M2, null),
                new Edge('\0', State.A, (sb,c) => sb.Append("/" + c) )  // We got a '/' not followed by a '*', so append the '/' and whatever follows it
                } },
            { State.M2, new Edge[] {
                new Edge( '*', State.M3, null),
                new Edge('\0', State.M2, null)
                } },
            { State.M3, new Edge[] {
                new Edge( '/', State.A, null),
                new Edge( '*', State.M3, null),
                new Edge('\0', State.M2, null)
                } },
            { State.S1, new Edge[] {
                new Edge( '-', State.S2, null),
                new Edge('\0', State.A, (sb,c) => sb.Append("-" + c) )  // We got a '-' not followed by a '-', so append the '-' and whatever follows it
                } },
            { State.S2, new Edge[] {
                new Edge( '\n', State.A, null),
                new Edge('\0', State.S2, null)
            } },
        };

        static State Transition(StringBuilder sb, State state, char c)
        {
            if (transitions.TryGetValue(state, out Edge[] edges) == false)
                throw new ArgumentException("Unknown state: " + state.ToString());

            // since states only have a few edges, a linear search is fine here
            foreach (Edge e in edges)
            {
                if (e.C == '\0' || e.C == c)         // '\0' is our wildcard character, it represents the default transition
                {
                    e.Action?.Invoke(sb, c);        // perform the action if there is one

                    return e.Next;
                }
            }

            throw new ArgumentException($"There is no edge for char {c} in state {state}");
        }


        public static string ProcessSql(string sqlWithComments)
        {
            StringBuilder sb = new StringBuilder();

            State state = State.A;  // start in the start state

            foreach (char c in sqlWithComments)
            {
                State newstate = Transition(sb, state, c);

                state = newstate;
            }

            if (state == State.M2 || state == State.M3)
                throw new Exception("Unterminated multi-line comment");

            // If the file ends on a - or /, we should preserve that (even though it is very likely invalid SQL)
            if (state == State.S1)
                sb.Append("-");
            else if (state == State.M1)
                sb.Append("/");

            string sqlNoComments = sb.ToString();

            return sqlNoComments;
        }
    }
} 