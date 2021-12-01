using System;
using System.Collections.Generic;
using InfoMarkup;

namespace InfoMarkupTester
{
    class Program
    {
        public static void Main(string[] args)
        {
            string path;
            
            if (args.Length < 1)
            {
                Console.Write("Enter .info file path: ");
                path = Console.ReadLine();
            }
            else
                path = args[0];

            InfoMarkupConfiguration config = new InfoMarkupConfiguration();
            config.AddUnitScale("s", 1.0f);
            config.AddUnitScale("ms", 0.001f);

            InfoMarkupValidator validator = new InfoMarkupValidator();
            validator.SetElementRules("moveset", "string", "/", "");
            validator.SetElementRules("move", "string?,string?", "moveset,move,move*", "startup,evade,impact,duration,recovery,chain,input,damage,poise,stamina,anim,can_cancel,is_starter,type");
            validator.SetElementRules("move*", "string?,string?", "moveset,move,move*", "startup,evade,impact,duration,recovery,chain,input,damage,poise,stamina,anim,can_cancel,is_starter,type");
            validator.SetElementRules("stance", "string?,string?", "moveset", "startup,chain");
            validator.SetElementRules("event", "string|group", "move,stance", "apply");
            validator.SetElementRules("effect", "string", "moveset", "duration,limit,force,disable");
            validator.SetElementRules("interval", "number", "effect", "");
            //validator.SetElementRules("nearby_ally", "number", "interval", "damage");

            validator.SetElementRules("appearance", "string", "/", "");
            validator.SetElementRules("tints", "", "", "");
            validator.SetElementRules("group", "", "", "");
            validator.SetElementRules("segment", "string,string,number", "", "");
            validator.SetElementRules("armorset", "string,string", "", "");
            validator.SetElementRules("armorset*", "string,string", "", "");
            validator.SetElementRules("armor", "string,string", "armorset,armorset*", "model");
            validator.SetElementRules("material", "string,string", "armor", "");
            validator.SetElementRules("pattern", "", "armor", "");
            validator.SetElementRules("design", "", "armor", "");

            InfoMarkupReader reader = new InfoMarkupReader(config);

            reader.SetValidator(validator);

            int pCount;

            if (reader.Open(path))
            {
                Console.WriteLine("File '{0}' opened successfully", path);

                while (reader.ReadValidated())
                {
                    if (reader.currentTag == "moveset" && reader.currentNodeType == NodeType.ElementStart)
                    {
                        Moveset moveset = new Moveset();
                        moveset.Load(reader);
                        Console.WriteLine(moveset.ToString());

                        continue;
                    }

                    for (int i = 0, s = reader.currentDepth; i < s; ++i)
                        Console.Write('\t');

                    switch (reader.currentNodeType)
                    {
                        case NodeType.ElementStart:
                            Console.Write("Element: " + reader.currentTag);
                            Console.Write(" - Start (");

                            pCount = reader.GetParameterCount();

                            if (pCount > 0)
                            {
                                Console.Write(reader.GetParameter(0).GetString(""));
                                for (int p = 1; p < pCount; ++p)
                                    Console.Write(", " + reader.GetParameter(p).GetString(""));
                            }
                            else
                                Console.Write("No Parameters");

                            Console.Write(")");
                            break;

                        case NodeType.ElementClose:
                            Console.Write("Element: " + reader.currentTag);
                            Console.Write(" - Close (");

                            pCount = reader.GetParameterCount();

                            if (pCount > 0)
                            {
                                Console.Write(reader.GetParameter(0).GetString(""));
                                for (int p = 1; p < pCount; ++p)
                                    Console.Write(", " + reader.GetParameter(p).GetString(""));
                            }
                            else
                                Console.Write("No Parameters");

                            Console.Write(")");
                            break;

                        case NodeType.Attribute:
                            Console.Write("Attribute: " + reader.currentKey + " - " + reader.currentValue.GetString());
                            break;

                        case NodeType.Option:
                            Console.Write("Option: " + reader.currentValue.GetString());
                            break;
                    }

                    Console.WriteLine();
                }

                reader.Close();
            }
            else
                Console.WriteLine(string.Format("Error trying to open file '{0}'", path));

            Console.ReadKey();
        }

        public class Moveset
        {
            private Move[] moveset;

            private class Transition
            {
                public Move pendingMove;
                public float branchTiming;
                public bool onSuccessOnly;

                public override string ToString()
                {
                    return string.Format("Chain to '{0}' with branch timing: {1}", pendingMove?.name ?? "Missing", branchTiming);
                }
            }

            private class Move
            {
                public string name;
            }

            private class MoveAttack : Move
            {
                public char direction;
                public float hitDamage;
                public float hitPoise;
                public float useStamina;
                public float startup;
                public float impact;
                public float duration;
                public float recovery;
                public Transition[] transitions;
                public string animationState;
                public string input;

                public override string ToString()
                {
                    string str = string.Format("Move '{0}' - startup: {1}, impact: {2}, damage: {3}, direction: {4}\n", name, startup, impact, hitDamage, direction.ToString().ToUpper());
                    
                    if (transitions != null)
                        foreach (Transition t in transitions)
                            str += "\t" + t.ToString() + "\n";

                    return str;
                }
            }

            private class MoveStance : Move
            {

            }

            private class Resolver
            {
                public class Pair
                {
                    public List<Transition> list;
                    public Move existingMove;
                }

                public Dictionary<string, Pair> map;

                public Resolver()
                {
                    map = new Dictionary<string, Pair>();
                }

                public Move Link(Transition transition, string key)
                {
                    Pair obj;
                    
                    if (!map.TryGetValue(key, out obj))
                    {
                        obj                 = new Pair();
                        obj.list            = new List<Transition>();
                        obj.existingMove    = null;

                        map.Add(key, obj);
                    }

                    if (obj.existingMove != null)
                        return obj.existingMove;

                    obj.list.Add(transition);
                    return null;
                }

                public void Resolve(string key, Move move)
                {
                    Pair obj;

                    if (map.TryGetValue(key, out obj))
                    {
                        if (obj.list != null)
                        {
                            foreach (Transition item in obj.list)
                                item.pendingMove = move;

                            obj.list = null;
                        }

                        obj.existingMove = move;
                    }
                    else
                    {
                        obj                 = new Pair();
                        obj.list            = null;
                        obj.existingMove    = move;

                        map.Add(key, obj);
                    }
                }
            }

            public bool Load(InfoMarkupReader reader)
            {
                if (!reader.LockScope("moveset"))
                    return false;

                List<Move> moves    = new List<Move>();
                Resolver resolver   = new Resolver();
                MoveAttack attack;
                char direction;
                string name;
                string type;

                HashSet<string> keywords = new HashSet<string>();
                keywords.Add("l");
                keywords.Add("r");
                keywords.Add("u");
                keywords.Add("d");

                while (reader.ReadValidated())
                {
                    direction = '0';
                    name = "";

                    switch (reader.currentNodeType)
                    {
                        case NodeType.ElementClose:
                            if (reader.currentTag == "move" || reader.currentTag == "move*")
                            {
                                foreach (InfoValue p in reader.ParameterValues())
                                {
                                    string s = p.GetString();

                                    if (keywords.Contains(s))
                                    {
                                        if (s.Length == 1)
                                            direction = s[0];
                                    }
                                    else
                                        name = s;
                                }

                                type = reader.GetAttribute("type").GetString(null);

                                if (!(reader.subElementCount < 1 || !string.IsNullOrWhiteSpace(name) || reader.currentTag.EndsWith("*"))) // move must be named, a leaf (no subelements), or marked as an opener "move*"
                                    continue;

                                if (type != null)
                                    name = "(" + type + " - " + direction + ")";
                                else if (string.IsNullOrWhiteSpace(name))
                                    name = "Unnamed " + (moves.Count + 1);

                                attack = new MoveAttack();

                                attack.name         = name;
                                attack.direction    = direction;

                                attack.startup      = reader.GetAttribute("startup").GetFloat();
                                attack.impact       = reader.GetAttribute("impact").GetFloat();
                                attack.duration     = reader.GetAttribute("duration").GetFloat();
                                attack.recovery     = reader.GetAttribute("recovery").GetFloat();
                                attack.hitDamage    = reader.GetAttribute("damage").GetFloat();
                                attack.hitPoise     = reader.GetAttribute("poise").GetFloat();
                                attack.useStamina   = reader.GetAttribute("stamina").GetFloat();

                                attack.input            = reader.GetAttribute("input").GetString();
                                attack.animationState   = reader.GetAttribute("anim").GetString();

                                InfoValue chainValues   = reader.GetAttribute("chain");
                                int chainCount          = chainValues.GetCount();

                                if (chainCount > 0)
                                {
                                    attack.transitions = new Transition[chainCount];

                                    for (int i = 0; i < chainCount; ++i)
                                    {
                                        Transition transition = new Transition();

                                        transition.onSuccessOnly    = false;
                                        transition.branchTiming     = chainValues.GetSubValue(i).GetFloatAt(1, 0.0f);
                                        transition.pendingMove      = resolver.Link(transition, chainValues.GetSubValue(i).GetStringAt(0));

                                        attack.transitions[i] = transition;
                                    }
                                }
                                else
                                    attack.transitions = null;

                                moves.Add(attack);

                                if (!string.IsNullOrWhiteSpace(name))
                                    resolver.Resolve(name, attack);
                            }
                            break;
                    }
                }

                moveset = moves.ToArray();

                return true;
            }

            public override string ToString()
            {
                string str = "";
                foreach (Move move in moveset)
                    str += move.ToString() + "\n";

                return str;
            }
        }
    }
}
