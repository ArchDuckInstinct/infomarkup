using System;
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

            InfoMarkupReader reader = new InfoMarkupReader(config);

            if (reader.Open(path))
            {
                Console.WriteLine("File '{0}' opened successfully", path);

                while (reader.Read())
                {
                    for (int i = 0, s = reader.currentDepth; i < s; ++i)
                        Console.Write('\t');

                    switch (reader.type)
                    {
                        case InfoType.ElementStart:
                            Console.Write("Element: " + reader.currentTag);
                            Console.Write(" - Start");
                            break;

                        case InfoType.ElementClose:
                            Console.Write("Element: " + reader.currentTag);
                            Console.Write(" - Close");
                            break;

                        case InfoType.Attribute:
                            Console.Write("Attribute: " + reader.currentKey + " - " + reader.currentValue.GetString());
                            break;

                        case InfoType.Option:
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
    }
}
