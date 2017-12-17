using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;

namespace Crammer {
    public sealed class CrammerUI {
        private static readonly CrammerUI instance = new CrammerUI();
        private const string buildName = "ion", buildVersion = "03", buildColorCharacter = "y";

        private CrammerUI() {}

        // Program boot -- displays a welcome message and enters the user input loop
        public static void Main() {
            Console.Clear();
            CrammerIO.LoadStudysets();
            CrammerIO.Write("&wWelcome to &bCrammer&w [Version &" + buildColorCharacter + buildName + "." + buildVersion + "&w]\nCreated by Stefan deBruyn\n\n");
            instance.Run();
        }

        // User input loop preceded by a help message and broken by entering the command "exit"
        public void Run() {
            CrammerIO.Write("&lType \"?\" for help.&w\n\n");

            string input;
            do {
                CrammerIO.Write("> ");
                input = Console.ReadLine();
                Command.Enter(Regex.Split(input.TrimEnd('\r', '\n'), @"\s+"));
            } while (input != "exit");
        }
    }

    /*
     * Used for all UI output. Color-coding enabled
     */
    public sealed class CrammerIO {
        // Color code mapping; in color-coded strings, an ampersand followed by a letter denotes a color change
        private static readonly Dictionary<string,ConsoleColor> colorCharacters = new Dictionary<string,ConsoleColor>() {
            {"r", ConsoleColor.Red},
            {"b", ConsoleColor.Blue},
            {"w", ConsoleColor.White},
            {"y", ConsoleColor.Yellow},
            {"l", ConsoleColor.Green},
            {"c", ConsoleColor.Cyan}
        };

        private CrammerIO() {}
        
        // Like Console.WriteLine(), but routed through CrammerIO.Write() and therefore capable of color-coding
        public static void WriteLine(string str) { Write("\n" + str); }

        // Writing variation that appends two newlines, creating a one-line gutter between this writing and the next
        public static void WriteGutter(string str) { Write(str + "\n\n"); }

        // Standard string writing. Color-coding enabled
        public static void Write(string str) {
            int pos = 0;

            while (pos < str.Length) {
                if (str.Substring(pos, 1) == "&") {
                    Console.ForegroundColor = colorCharacters[str.Substring(pos+1, 1)];
                    pos += 2;
                    continue;
                }
                System.Console.Write(str.Substring(pos,1));
                pos++;
            }

            Console.ForegroundColor = System.ConsoleColor.White;
            Console.BackgroundColor = System.ConsoleColor.Black;
        }

        // Save all studysets to the local directory
        public static void SaveStudysets() {
            foreach (Studyset set in CrammerDB.studysets) {
                string path = set.name + ".set";

                // Clear the file if it already exists
                if (File.Exists(path))
                    File.WriteAllText(path, "");

                // Write studyset contents to file
                foreach (Notecard n in set.GetTerms())
                    File.AppendAllText(path, n.term + "=" + n.definition + "\n");
            }
        }

        // Load all studysets from the local directory
        public static void LoadStudysets() {
            foreach (string path in Directory.GetFiles(Directory.GetCurrentDirectory(), "*.set")) {
                Studyset newSet = new Studyset(Path.GetFileNameWithoutExtension(path));

                foreach (string line in File.ReadLines(path)) {
                    string term = line.Substring(0, line.IndexOf("="));
                    string def = line.Substring(line.IndexOf("=")+1, line.Length-line.IndexOf("=")-1);
                    newSet.AddTerm(term, def);
                }

                CrammerDB.AddStudyset(newSet);
            }
        }
    }

    /*
     * Executes all user commands
     */
    public sealed class Command {
        // Wrapper for command syntax and usage explanation, used in lookup.
        private struct CommandInfo {
            public readonly string[] usage;
            public readonly string help;

            public CommandInfo(string[] u, string h) { usage = u; help = h; }
        }

        private static readonly Dictionary<string,CommandInfo> commandDirectory = new Dictionary<string,CommandInfo>() {
            {"?",     new CommandInfo(new[] {"?"},                   "view command directory")},
            {"exit",  new CommandInfo(new[] {"exit"},                "save and exit")},
            {"new",   new CommandInfo(new[] {"new", "<name>"},       "create a new studyset")},
            {"study", new CommandInfo(new[] {"study", "<studyset>"}, "study notecards from a studyset in a random order")},
            {"edit",  new CommandInfo(new[] {"edit", "<studyset>"},  "add or remove notecards from a studyset")},
            {"list",  new CommandInfo(new[] {"list", "<studyset>"},  "view all notecards in a studyset")},
            {"sets",  new CommandInfo(new[] {"sets"},                "view all studysets")}
        };

        private Command() {}

        // Takes command entries in array form, where the first index is the command keyword and the following indices
        // are any parameters
        public static void Enter(string[] entry) {
            try {
                if (!VerifyUsage(entry, commandDirectory[entry[0]].usage))
                    return;
            } catch (KeyNotFoundException) {
                CrammerIO.WriteGutter("&rCommand not recognized.");
                return;
            }

            // Scratch references used in multiple cases
            Studyset sourceSet;
            string input = "";
            bool success;

            switch (entry[0]) {
                // ? - help command
                case "?":
                    foreach (string str in commandDirectory.Keys.ToList())
                        CrammerIO.Write("&y" + string.Join(" ", commandDirectory[str].usage) + " - " + commandDirectory[str].help + "\n");

                    CrammerIO.Write("\n");
                break;

                // new - create a new studyset
                case "new":
                    // Verify new studyset name availability
                    success = true;

                    foreach (Studyset set in CrammerDB.studysets)
                        if (set.name == entry[1]) {
                            success = false;
                            CrammerIO.WriteGutter("&rThat name is already in use!");
                            break;
                        }

                    if (!success)
                        break;

                    // Add terms to new studyset
                    Studyset newSet = new Studyset(entry[1]);
                    CrammerIO.WriteGutter("&c" + entry[1] + " studyset created.&w Now it needs some contents.\n" +
                                    "&yAdd notecards by entering \"<term> - <definition>\". Enter \"done\" when finished.");

                    do {
                        input = Console.ReadLine();
                        if (input.ToLower() != "done") {
                            // Enforce syntax
                            if (!input.Contains("-")) {
                                CrammerIO.Write("&rImproper syntax; try <term> - <definition>\n");
                                continue;
                            }

                            // Syntax was OK--add new notecard
                            string term = input.Substring(0, input.IndexOf("-")).Trim();
                            string def = input.Substring(input.IndexOf("-")+1, input.Length-input.IndexOf("-")-1).Trim();
                            newSet.AddTerm(term, def);
                            CrammerIO.Write("&cAdded " + term + ".\n");
                        }
                    } while (input.ToLower() != "done");

                    CrammerDB.AddStudyset(newSet);
                    CrammerIO.SaveStudysets();
                    CrammerIO.WriteGutter("\n&c" + newSet.GetLength() + " notecards added to " + newSet.name + ".");
                break;

                // study - study terms from a studyset
                case "study":
                    // Verify that the studyset specified for studying exists
                    sourceSet = null;
                    success = false;

                    foreach (Studyset set in CrammerDB.studysets)
                        if (set.name == entry[1]) {
                            sourceSet = set;
                            success = true;
                            break;
                        }

                    if (!success) {
                        CrammerIO.WriteGutter("&rStudyset not found.");
                        break;
                    }

                    // Verify that the specified studyset has contents
                    if (sourceSet.GetLength() == 0) {
                        CrammerIO.WriteGutter("&rThat studyset has no notecards.");
                        break;
                    }

                    // Begin studying
                    Console.Clear();
                    CrammerIO.WriteGutter("&cOpened " + entry[1] + " for studying.\n" + 
                                          "&yEnter \"done\" when finished.");

                    List<Notecard> pool = sourceSet.CloneTerms();

                    do {
                        if (input.ToLower() != "done") {
                            // Display a random notecard and remove it from the pool
                            Notecard note = pool[new Random().Next(pool.Count)];
                            pool.Remove(note);
                            CrammerIO.Write("&c" + note.term + " ");

                            // If the pool is empty, refill it
                            if (pool.Count == 0)
                                pool = sourceSet.CloneTerms();

                            // Wait for user to continue
                            input = Console.ReadLine();

                            // Display the answer
                            if (input.ToLower() != "done") {
                                string defCol = input == note.definition ? "l" : "w";
                                CrammerIO.Write("  &l> &" + defCol + note.definition + "\n");
                            }
                        }
                    } while (input != "done");

                    CrammerIO.Write("\n");
                break;

                // edit - edit a studyset
                case "edit":
                    // Verify that the specified studyset exists
                    sourceSet = null;
                    success = false;

                    foreach (Studyset set in CrammerDB.studysets)
                        if (set.name == entry[1]) {
                            sourceSet = set;
                            success = true;
                            break;
                        }

                    if (!success) {
                        CrammerIO.WriteGutter("&rStudyset not found.");
                        break;
                    }

                    // Enter studyset editor
                    CrammerIO.WriteGutter("&cOpened " + entry[1] + " for editing.\n" +
                                          "&yEnter \"add <term> - <definition>\" to add a notecard, or \"remove <term>\" to delete one. Return with \"done\".");
                    
                    do {
                        input = Console.ReadLine();
                        if (input != "done") {
                            string[] inputEntry = Regex.Split(input.TrimEnd('\r', '\n'), @"\s+");

                            // Add notecard to pre-existing studyset
                            if (inputEntry[0] == "add") {
                                // Enforce syntax
                                if (inputEntry.Length < 4) {
                                    CrammerIO.Write("&rImproper syntax; try add <term> - <definition>\n");
                                    continue;
                                }

                                // Syntax was OK -- add new notecard
                                string flatParing = "";

                                for (int i = 1; i < inputEntry.Length; i++)
                                    flatParing += inputEntry[i] + " ";

                                string newTerm = flatParing.Substring(0, flatParing.IndexOf("-")).Trim();
                                string newDef = flatParing.Substring(flatParing.IndexOf("-")+1, flatParing.Length-flatParing.IndexOf("-")-1).Trim();

                                sourceSet.AddTerm(newTerm, newDef);
                                CrammerIO.Write("&cAdded " + newTerm + ".\n");

                            // Remove notecard from pre-existing studyset
                            } else if (inputEntry[0] == "remove") {
                                // Enforce syntax
                                if (inputEntry.Length < 2) {
                                    CrammerIO.Write("&rImproper syntax; try remove <term>\n");
                                    continue;
                                }

                                // Verify that the specified notecard exists
                                success = false;

                                string flatName = "";

                                for (int i = 1; i < inputEntry.Length; i++)
                                    flatName += inputEntry[i] + " ";

                                flatName = flatName.Trim();

                                foreach (Notecard n in sourceSet.GetTerms())
                                    if (n.term == flatName) {
                                        success = true;
                                        break;
                                    }

                                if (!success) {
                                    CrammerIO.Write("&rNotecard not found.\n");
                                    continue;
                                }

                                // Remove specified notecard from the list
                                sourceSet.RemoveTerm(flatName);
                                CrammerIO.Write("&rRemoved " + flatName + ".\n");

                            // Studyset editor command not found
                            } else {
                                CrammerIO.Write("&rCommand not found.\n");
                            }
                        }
                    } while (input != "done");

                    CrammerIO.SaveStudysets();
                    CrammerIO.WriteGutter("\n&cChanges saved.");
                break;

                // list - view a studyset
                case "list":
                    // Verify that the specified studyset exists
                    sourceSet = null;
                    success = false;

                    foreach (Studyset set in CrammerDB.studysets)
                        if (set.name == entry[1]) {
                            sourceSet = set;
                            success = true;
                            break;
                        }

                    if (!success) {
                        CrammerIO.WriteGutter("&rStudyset not found.");
                        break;
                    }

                    // Verify that the specified studyset for listing has contents
                    if (sourceSet.GetLength() == 0) {
                        CrammerIO.WriteGutter("&rThat studyset has no notecards.");
                        break;
                    }

                    // List notecards
                    foreach (Notecard n in sourceSet.GetTerms())
                        CrammerIO.Write("&b" + n.term + "&c - &w" + n.definition + "\n");

                    CrammerIO.Write("\n");
                break;

                // sets - list all studyset names
                case "sets":
                    // Verify that there are any studysets
                    if (CrammerDB.studysets.Count == 0) {
                        CrammerIO.WriteGutter("&rYou have no studysets.");
                        break;
                    }
 
                    // Print all studyset names
                    foreach (Studyset set in CrammerDB.studysets)
                        CrammerIO.Write("&c" + set.name + "\n");

                    CrammerIO.Write("\n");
                break;
            }
        }

        // Returns whether or not the number of parameters entered is valid for a specific usage. If it isn't, the usage
        // is printed
        private static bool VerifyUsage(string[] entry, string[] usage) {
            if (entry.Length != usage.Length) {
                CrammerIO.WriteGutter("&rImproper syntax; try " + string.Join(" ",usage));
                return false;
            }

            return true;
        }
    }

    /*
     * A term-definition paring, like a vocab word
     */
    public struct Notecard {
        public readonly string term, definition;

        public Notecard(string t, string d) { term = t; definition = d; }

        public override string ToString() { return term + " - " + definition; }

        public override bool Equals(object obj) {
            if (obj == null || GetType() != obj.GetType())
                return false;

            return (term == ((Notecard)obj).term);
        }

        public override int GetHashCode() {
            string str = "";

            foreach (char c in term)
                str += (int)c;

            return Int32.Parse(str);
        }
    }

    /*
     * A collection of notecards with various helper methods
     */
    public sealed class Studyset {
        public readonly string name;
        private List<Notecard> studyset = new List<Notecard>();

        public Studyset(string n) { name = n; }

        public void AddTerm(string term, string def) { studyset.Add(new Notecard(term, def)); }

        public List<Notecard> CloneTerms() {
            List<Notecard> clones = new List<Notecard>();

            foreach (Notecard note in studyset)
                clones.Add(new Notecard(note.term, note.definition));

            return clones;
        }

        public int GetLength() { return studyset.Count; }

        public Notecard GetRandomTerm() { return studyset[new Random().Next(studyset.Count)]; }

        public List<Notecard> GetTerms() { return studyset; }

        public void RemoveTerm(string term) { studyset.Remove(new Notecard(term, "")); }
    }

    /*
     * Public storage for all of the user's data
     */
     public sealed class CrammerDB {
        public static List<Studyset> studysets = new List<Studyset>();

        public static void AddStudyset(Studyset set) { studysets.Add(set); }

        public static Studyset GetStudyset(string name) {
            foreach (Studyset set in studysets)
                if (set.name == name)
                    return set;

            return null;
        }

        public static void RemoveStudyset(string name) {
            foreach (Studyset set in studysets)
                if (set.name == name) {
                    studysets.Remove(set);
                    break;
                }
        }
     }
}