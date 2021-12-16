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
            validator.SetElementRules("move", "?string,string", "moveset,move,move*", "startup,evade,impact,duration,recovery,chain,input,damage,poise,stamina,anim,can_cancel,is_starter,type");
            validator.SetElementRules("move*", "?string,string", "moveset,move,move*", "startup,evade,impact,duration,recovery,chain,input,damage,poise,stamina,anim,can_cancel,is_starter,type");
            validator.SetElementRules("stance", "?string,string", "moveset", "startup,chain");
            validator.SetElementRules("event", "string|group", "move,stance", "apply");
            validator.SetElementRules("effect", "string", "moveset", "duration,limit,force,disable");
            validator.SetElementRules("interval", "number", "effect", "");

            validator.SetElementRules("appearance", "string", "/", "");
            validator.SetElementRules("tint_labels", "", "appearance", "");
            validator.SetElementRules("tint_group", "string", "appearance", "");
            validator.SetElementRules("segment", "string,string,number", "appearance", "");
            validator.SetElementRules("armorset", "string,string", "appearance", "");
            validator.SetElementRules("armorset*", "string,string", "appearance", "");
            validator.SetElementRules("armor", "string,string", "armorset,armorset*", "model,material,patterns,designs");

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
                        reader.LockScope();

                        Moveset moveset = new Moveset();
                        moveset.Load(reader);
                        Console.WriteLine(moveset.ToString());
                    }
                    else if (reader.currentTag == "appearance" && reader.currentNodeType == NodeType.ElementStart)
                    {
                        reader.LockScope();

                        Appearance appearance = new Appearance();
                        appearance.Load(reader);
                        Console.WriteLine(appearance.ToString());
                    }
                    else
                    {
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

            public bool Load(InfoMarkupReader reader)
            {
                InfoMarkupResolver<Move, Transition> resolver = new InfoMarkupResolver<Move, Transition>((Move m, Transition t) => {t.pendingMove = m;});

                List<Move> moves = new List<Move>();
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
                                        
                                        resolver.Link(chainValues.GetSubValue(i).GetStringAt(0), transition);

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

        public class Appearance
        {
            private class TintLabels
            {
                public Dictionary<string, string> options;
            }

            private class TintGroup
            {
                public string[] options;
            }

            private class Segment
            {
                public string tag;
                public string label;
                public int index;
                public List<Armor> armors;
            }

            private class Armorset
            {
                public string name;
                public string assetPath;
                public bool isDefault;
            }

            private class ArmorMaterial
            {
                public string hexDefault;
                public TintGroup available;
            }

            private class Armor
            {
                public Armorset set;
                public string model;
                public ArmorMaterial[] materials;
                public int patternCount;
                public int designCount;
            }

            Segment[] segments;

            public Appearance()
            {
                segments = null;
            }

            private static void ResolveTintGroup(TintGroup t, ArmorMaterial a)
            {
                a.available = t;
            }

            private static void ResolveSegment(Segment s, Armor a)
            {
                s.armors.Add(a);
            }

            public bool Load(InfoMarkupReader reader)
            {
                InfoMarkupResolver<TintGroup, ArmorMaterial> tintResolver   = new InfoMarkupResolver<TintGroup, ArmorMaterial>(ResolveTintGroup);
                InfoMarkupResolver<Segment, Armor> segmentResolver          = new InfoMarkupResolver<Segment, Armor>(ResolveSegment);

                List<Segment> segmentList = new List<Segment>();

                Armorset armorset;
                Armor armor;
                Segment segment;

                while (reader.ReadValidated())
                {
                    if (reader.currentNodeType == NodeType.ElementStart)
                    {
                        if (reader.currentTag == "armorset")
                        {
                            armorset = new Armorset();

                            armorset.name       = reader.GetParameter(0).GetString();
                            armorset.assetPath  = reader.GetParameter(1).GetString();

                            reader.LockScope();
                            while (reader.ReadValidated())
                            {
                                if (reader.currentNodeType == NodeType.ElementClose && reader.currentTag == "armor")
                                {
                                    armor = new Armor();

                                    armor.set           = armorset;
                                    armor.model         = reader.GetAttribute("model").GetString();
                                    armor.designCount   = reader.GetAttribute("designs").GetInteger();
                                    armor.patternCount  = reader.GetAttribute("patterns").GetInteger();

                                    InfoValue materials = reader.GetAttribute("material");
                                    int materialCount   = materials.GetCount();

                                    armor.materials = new ArmorMaterial[materialCount];
                                    
                                    for (int i = 0; i < materialCount; ++i)
                                    {
                                        ArmorMaterial m = new ArmorMaterial();

                                        // TODO: Implement hex info value type
                                        m.hexDefault = materials.GetSubValue(i).GetStringAt(1);
                                        tintResolver.Link(materials.GetSubValue(i).GetStringAt(0), m);

                                        armor.materials[i] = m;
                                    }

                                    segmentResolver.Link(reader.GetParameter(0).GetString(), armor);
                                }
                            }

                            armorset.isDefault = reader.OptionsContain("default");
                        }
                    }

                    if (reader.currentNodeType == NodeType.ElementClose)
                    {
                        switch (reader.currentTag)
                        {
                            case "tint_labels":
                                TintLabels labels   = new TintLabels();
                                labels.options      = new Dictionary<string, string>();
                                reader.CopyOptionsTo(labels.options);
                                break;

                            case "tint_group":
                                TintGroup group = new TintGroup();
                                group.options   = reader.OptionsToArray();

                                tintResolver.Resolve(reader.GetParameter(0).GetString(), group);
                                break;

                            case "segment":
                                segment         = new Segment();
                                segment.armors  = new List<Armor>();
                                segment.tag     = reader.GetParameter(0).GetString();
                                segment.label   = reader.GetParameter(1).GetString();
                                segment.index   = reader.GetParameter(2).GetInteger();

                                segmentList.Add(segment);

                                segmentResolver.Resolve(segment.tag, segment);
                                break;
                        }
                    }
                }

                segments = segmentList.ToArray();

                return true;
            }

            public override string ToString()
            {
                string str = "";

                foreach (Segment s in segments)
                {
                    str += $"Segment - {s.tag}\n";

                    foreach (Armor a in s.armors)
                    {
                        str += $"\t {a.set.name}";
                        if (a.set.isDefault)
                            str += "*";
                        str += "\n";

                        foreach (ArmorMaterial m in a.materials)
                            str += $"\t\t{m.hexDefault} ";
                        str += "\n";
                    }
                }

                return str;
            }
        }
    }
}
