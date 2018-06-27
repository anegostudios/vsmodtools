using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vsmodtools
{
    public class Patcher
    { 

        public List<Patch> Patches = new List<Patch>();

        public Patcher(params Patch[] patches)
        {
            this.Patches.AddRange(patches);
        }

        public bool Patch(string filename)
        {
            Console.WriteLine("Patching " + Path.GetFileName(filename)  + " ...");

            List<string> lines = new List<string>(File.ReadAllLines(filename));

            Patches.ForEach(x => x.patched = false);

            List<Patch> patched = new List<Patch>();
            for(int i = 0; i < lines.Count; i++)
            {
                foreach(var patch in Patches)
                {
                    var line = lines[i];
                    bool? result = patch.ScanLine(lines, line, i);
                    patch.patched = true;
                    if (result == false)
                        Console.WriteLine("{0}\n Failed to inject patch at line {1}!", line, i);
                }
            }

            if (Patches.Find(x => !x.patched) != null)
            {
                foreach (var patch in Patches)
                {
                    if(!patch.patched)
                        Console.WriteLine("Could not inject patch {0}!", patch);
                }
                return false;
            }

            File.WriteAllLines(filename, lines);
            return true;
        }

    }

    public abstract class Patch
    {
        internal bool patched = false;

        public static bool StartsWith(string input, string pattern)
        {
            return input.Trim().StartsWith(pattern, StringComparison.OrdinalIgnoreCase);
        }

        public static string GetLineSpacing(string line)
        {
            for (int i = 0; i < line.Length; i++)
            {
                char charAt = line.ElementAt(i);
                if (charAt != '\t' && charAt != ' ')
                    return line.Substring(0, i);
            }
            return "";
        }

        public abstract bool? ScanLine(List<string> lines, string line, int index);

    }

    public class LinePatch : Patch
    {
        public string StartPattern;
        public string Replace;

        public LinePatch(string startPattern, string replace)
        {
            this.StartPattern = startPattern;
            this.Replace = replace;
        }

        public override bool? ScanLine(List<string> lines, string line, int index)
        {
            if (StartsWith(line, StartPattern))
            {
                lines[index] = GetLineSpacing(line) + Replace;
                return true;
            }
            return null;
        }
    }

    public class ConditionedLinePatch : LinePatch
    {

        public string BracketStart;
        public string BracketEnd;

        public ConditionedLinePatch(string bracketStart, string startPattern, string replace, string bracketEnd) : base(startPattern, replace)
        {
            this.BracketStart = bracketStart;
            this.BracketEnd = bracketEnd;
        }

        public override bool? ScanLine(List<string> lines, string line, int index)
        {
            if (StartsWith(line, BracketStart))
            {
                for(int i = index; i < lines.Count; i++)
                {
                    line = lines[i];

                    if (StartsWith(line, BracketEnd))
                        return false;

                    bool? result = base.ScanLine(lines, line, i);
                    if (result != null)
                        return result;
                }
            }
            return null;
        }

    }

    public class InBetweenPatch : Patch
    {

        public string Start;
        public string End;
        public string[] InsertLines;

        public InBetweenPatch(string start, string end, params string[] lines)
        {
            this.Start = start;
            this.End = end;
            this.InsertLines = lines;
        }

        public override bool? ScanLine(List<string> lines, string line, int index)
        {
            if(StartsWith(line, Start))
            {
                int startLine = index;
                int endLine = -1;
                for(int i = index; i < lines.Count; i++)
                {
                    if(lines[i].Contains(End))
                    {
                        endLine = i;
                        break;
                    }
                }

                if (endLine == -1)
                    return false;

                string startString = lines[startLine];
                startString = startString.Substring(0, startString.IndexOf(Start) + Start.Length);

                string endString = lines[endLine];
                endString = endString.Substring(endString.IndexOf(End));

                if(startLine != endLine)
                    lines.RemoveRange(startLine + 1, endLine - startLine);

                if (InsertLines.Length > 0)
                    startString += InsertLines[0];
                if (InsertLines.Length <= 1)
                    startString += endString;
                lines[startLine] = startString;

                for(int i = 1; i < InsertLines.Length; i++)
                {
                    string toAdd = InsertLines[i];
                    if (i == InsertLines.Length - 1)
                        toAdd += End;
                    lines.Insert(startLine + 1, toAdd);
                }

                return true;
            }
            return null;
        }
    }
}
