using System;
using System.Collections.Generic;
using System.Linq;

public class State
{
    public int Id { get; set; }
    public string Name { get; set; }
    public List<Transition> Transitions { get; set; }
    public string Type { get; set; }
    public int ParrentId { get; set; }

    public State(int id, string name, string type, int parrentId)
    {
        Id = id;
        ParrentId = parrentId;
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
    public List<State> Analyze(int id, string plantUmlCode)
    {
        List<State> states = new List<State>();
        Stack<int> qParrent = new Stack<int>();
        qParrent.Push(-1);

        string[] lines = plantUmlCode.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
        foreach (string line in lines)
        {
            string trimmedLine = line.Trim();

            if(trimmedLine != string.Empty)
            {
                if (trimmedLine.StartsWith("state "))
                {
                    string stateName = trimmedLine.Substring(6, trimmedLine.IndexOf('{') - 6).Trim();

                    var sta = FindStateByName(states, stateName, qParrent.Peek());
                    if(sta == null)
                    {
                        sta = new State(++id, stateName, "State", qParrent.Peek());

                        states.Add(sta);
                        qParrent.Push(id);
                    }
                    else
                        qParrent.Push(sta.Id);
                }
                else if(trimmedLine.StartsWith("}"))
                {
                    // don't remove first parrent = -1
                    if(qParrent.Count > 1)
                        qParrent.Pop();
                }
                else if (trimmedLine.Contains("-->") 
                    || trimmedLine.Contains("->"))
                {
                    string[] parts = trimmedLine.Split(new[] { "-->", "->" }, StringSplitOptions.None);
                    string sourceStateName = parts[0].Trim();
                    string targetStateName = parts[1].Split(':')[0].Trim();

                    var split = parts[1].Split(':');
                    string @event = split.Length == 2 ? split[1].Trim() : null;

                    State sourceState = FindStateByName(states, sourceStateName, qParrent.Peek(), Direction.Source);
                    State targetState = FindStateByName(states, targetStateName, qParrent.Peek(), Direction.Target);

                    if (sourceState == null)
                    {
                        var type = "State";
                        if (sourceStateName == "[*]")
                            type = "Init";

                        sourceState = new State(++id, sourceStateName, type, qParrent.Peek());
                        states.Add(sourceState);
                    }

                    if (targetState == null)
                    {
                        var type = "State";
                        if (targetStateName == "[*]")
                            type = "Init";

                        targetState = new State(++id, targetStateName, type, qParrent.Peek());
                        states.Add(targetState);
                    }

                    Transition transition = new Transition(sourceState, targetState, @event);
                    sourceState.Transitions.Add(transition);
                }
            }    
        }

        return states;
    }



    private State FindStateByName(List<State> states, string stateName, int currentaParrentId, Direction? direction = null)
    {
        if(stateName != "[*]") 
            return states.Find(state => state.Name == stateName);
        else
        {
            if(direction == null 
                || direction == Direction.Source)
            {
                if (currentaParrentId != -1)
                    return null;
                else
                    return states.Find(state => state.Name == stateName && state.ParrentId == -1);
            }
            else
            {
                var exists = states.Where(state => state.ParrentId == currentaParrentId && state.Type == "Init")
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
scale 350 width
[*] --> NotShooting

state NotShooting {
  [*] --> Idle
  Idle --> Configuring : EvConfig
  Configuring --> Idle : EvConfig
}

state Configuring {
  [*] --> NewValueSelection
  NewValueSelection --> NewValuePreview : EvNewValue
  NewValuePreview --> NewValueSelection : EvNewValueRejected
  NewValuePreview --> NewValueSelection : EvNewValueSaved

  state NewValuePreview {

     State1 -> State2
     State2 -> [*]
  }

}
@enduml
        ";

        PlantUmlAnalyzer analyzer = new PlantUmlAnalyzer();
        List<State> states = analyzer.Analyze(0, plantUmlCode);

        foreach (State state in states)
        {
            Console.WriteLine("Id: " + state.Id);
            Console.WriteLine("State: " + state.Name);
            Console.WriteLine("Parrent Id: " + state.ParrentId);
            foreach (Transition transition in state.Transitions)
            {
                Console.WriteLine("Transition: " + transition.Source.Name + " --(" + transition.Event + ")--> " + transition.Target.Name);
            }
            Console.WriteLine();
        }

        Console.ReadLine();
    }
}