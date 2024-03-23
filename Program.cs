using System;
using System.Collections.Generic;
using System.Linq;

public class State
{
    public int Id { get; set; }
    public string Name { get; set; }
    public List<Transition> Transitions { get; set; }
    public string Type { get; set; }
    public int ParentId { get; set; }
    public string Content { get; set; }

    public State(int id, string name, string type, int parentId)
    {
        Id = id;
        ParentId = parentId;
        Type = type;
        Name = name;
        Transitions = new List<Transition>();
    }
}

public class Transition
{
    public State Source { get; set; }
    public State Target { get; set; }
    public string Event { get; set; }

    public Transition(State sourceState, State targetState, string @event)
    {
        Source = sourceState;
        Target = targetState;
        Event = @event;
    }
}

public class PlantUmlAnalyzer
{
    Dictionary<string, string> _variables = new Dictionary<string, string>();

    public List<State> Analyze(int id, string plantUmlCode)
    {
        List<State> states = new List<State>();
        Stack<int> qParent = new Stack<int>();
        qParent.Push(-1);

        string[] lines = plantUmlCode.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
        foreach (string line in lines)
        {
            string trimmedLine = line.Trim();

            if (trimmedLine != string.Empty)
            {
                // state ...
                if (trimmedLine.StartsWith("state "))
                {
                    #region Composite
                    // state State3 {
                    if (trimmedLine.IndexOf('{') > 0)
                    {
                        string stateName = trimmedLine.Substring(6, trimmedLine.IndexOf('{') - 6).Trim();

                        var sta = FindStateByName(states, stateName, qParent.Peek());
                        if (sta == null)
                        {
                            sta = new State(++id, stateName, "State", qParent.Peek());

                            states.Add(sta);
                            qParent.Push(id);
                        }
                        else
                            qParent.Push(sta.Id);
                    }
                    #endregion

                    #region State normal 
                    // state "Accumulate Enough Data\nLong State Name" as long1
                    else if (trimmedLine.Contains(" as "))
                    {
                        var splitVariables = GetVariable(trimmedLine.Substring(6), " as ");
                        _variables.Add(splitVariables.Item2, splitVariables.Item1);

                        // create state
                        State sourceState = FindStateByName(states, splitVariables.Item1, qParent.Peek(), Direction.Source);

                        if (sourceState == null)
                        {
                            var type = "State";
                            sourceState = new State(++id, splitVariables.Item1, type, qParent.Peek());
                            states.Add(sourceState);
                        }
                    }
                    #endregion
                }

                // }
                else if (trimmedLine.StartsWith("}"))
                {
                    // don't remove first parent = -1
                    if (qParent.Count > 1)
                        qParent.Pop();
                }

                // State3 --> State3 : Failed
                else if (trimmedLine.Contains("-->")
                    || trimmedLine.Contains("->"))
                {
                    State sourceState = null, targetState = null;
                    var currentParent = qParent.Peek();
                    string[] parts = trimmedLine.Split(new[] { "-->", "->" }, StringSplitOptions.None);
                    string sourceStateName = parts[0].Trim();
                    string targetStateName = parts[1].Split(':')[0].Trim();

                    var split = parts[1].Split(':');
                    string @event = split.Length == 2 ? split[1].Trim() : null;

                    #region Split history
                    // State2 --> State3[H*]: DeepResume
                    // State2 --> [H]: Resume
                    sourceState = TryFindState(states, sourceState, sourceStateName, currentParent, Direction.Source);
                    targetState = TryFindState(states, targetState, targetStateName, currentParent, Direction.Target);

                    #endregion

                    #region Create State
                    if (sourceState == null)
                    {
                        sourceState = CreateState(states, ++id, sourceStateName, currentParent);
                        states.Add(sourceState);
                    }    

                    if (targetState == null)
                    {
                        targetState = CreateState(states, ++id, targetStateName, currentParent);
                        states.Add(targetState);
                    }    
                    #endregion

                    Transition transition = new Transition(sourceState, targetState, @event);
                    sourceState.Transitions.Add(transition);
                }

                // long1 : Just a test
                else if (trimmedLine.Contains(":"))
                {
                    var splitVariables = GetVariable(trimmedLine, ":");

                    State sourceState = FindStateByName(states, splitVariables.Item1, qParent.Peek(), Direction.Source);
                    sourceState.Content += splitVariables.Item2;
                }
            }
        }

        return states;
    }

    private State CreateState(List<State> states, int id, string stateName, int parentId)
    {
        var type = "State";
        if (stateName == "[*]")
            type = "Init";

        // history
        if (HasHistoryInState(stateName))
        {
            var splitHistory = GetHistoryState(stateName);

            // splitHistory: state3 and [H]
            if (!string.IsNullOrEmpty(splitHistory.Item1))
            {
                var parent = states.FirstOrDefault(x => x.Name == splitHistory.Item1);
                if (parent != null)
                {
                    stateName = splitHistory.Item2;
                    parentId = parent.Id;
                }
            }
        }

        return new State(id, stateName, type, parentId);
    }

    private State TryFindState(List<State> states, State state, string stateName, int parentId, Direction? direction = null)
    {
        if (HasHistoryInState(stateName))
        {
            var splitHistory = GetHistoryState(stateName);

            // splitHistory: state3 and [H]
            if (!string.IsNullOrEmpty(splitHistory.Item1))
            {
                var parent = states.FirstOrDefault(x => x.Name == splitHistory.Item1);
                if (parent != null)
                {
                    stateName = splitHistory.Item2;
                    parentId = parent.Id;
                }
            }
        }

        state = FindStateByName(states, stateName, parentId, direction);
        return state;
    }

    private bool HasHistoryInState(string stateName)
        => stateName.Contains("[") && stateName.Contains("]") && stateName.Contains("H");

    private (string, string) GetHistoryState(string stateName)
    {
        var index = stateName.IndexOf("[");
        return (stateName.Substring(0, index).Trim(), stateName.Substring(index).Trim());
    }

    private (string, string) GetVariable(string input, string key)
    {
        var index = input.IndexOf(key);
        return (input.Substring(0, index).Trim(), input.Substring(index + key.Length).Trim());
    }

    private State FindStateByName(List<State> states, string stateName, int currentParentId, Direction? direction = null)
    {
        if (_variables.ContainsKey(stateName))
            stateName = _variables[stateName];

        if (stateName != "[*]")
            return states.Find(state => state.Name == stateName);
        else
        {
            if (direction == null
                || direction == Direction.Source)
            {
                if (currentParentId != -1)
                    return null;
                else
                    return states.Find(state => state.Name == stateName && state.ParentId == -1);
            }
            else
            {
                var exists = states.Where(state => state.ParentId == currentParentId && state.Type == "Init")
                    .OrderBy(state => state.Id).ToList();
                if (exists.Count() <= 1)
                    return null;
                else
                    return exists[1];
            }
        }
    }

    private State FindStateById(List<State> states, int id)
    {
        return states.Find(state => state.Id == id);
    }
}
public enum Direction
{
    Source,
    Target,
}
public class Program
{
    public static void Main()
    {
        string plantUmlCode = @"
@startuml

state fork_state <<fork>>
[*] --> fork_state
fork_state --> State2
fork_state --> State3

state join_state <<join>>
State2 --> join_state
State3 --> join_state
join_state --> State4
State4 --> [*]

@enduml
        ";

        PlantUmlAnalyzer analyzer = new PlantUmlAnalyzer();
        List<State> states = analyzer.Analyze(0, plantUmlCode);

        foreach (State state in states)
        {
            Console.WriteLine("Id: " + state.Id);
            Console.WriteLine("State: " + state.Name);
            Console.WriteLine("Parent Id: " + state.ParentId);
            if (!string.IsNullOrEmpty(state.Content))
                Console.WriteLine("Content: " + state.Content);
            foreach (Transition transition in state.Transitions)
            {
                Console.WriteLine("Transition: " + transition.Source.Name + " --(" + transition.Event + ")--> " + transition.Target.Name);
            }
            Console.WriteLine();
        }

        Console.ReadLine();
    }
}