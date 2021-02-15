﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Vivian.CodeAnalysis;
using Vivian.CodeAnalysis.Symbols;
using Vivian.CodeAnalysis.Syntax;
using Vivian.CodeAnalysis.Authoring;
using Vivian.IO;

namespace Vivian
{
    internal sealed class VivianRepl : Repl
    {
        private Compilation _previous;
        private bool _showTree;
        private bool _showProgram;
        private static bool _loadingSubmissions;
        private static readonly Compilation emptyCompilation = Compilation.CreateScript(null);
        
        private readonly Dictionary<VariableSymbol, object> _variables = new Dictionary<VariableSymbol, object>();

        public VivianRepl()
        {
            LoadSubmissions();
        }
        
        protected override object? RenderLine(IReadOnlyList<string> lines, int lineIndex, object state)
        {
            SyntaxTree syntaxTree;
            
            if (state == null)
            {
                var text = string.Join(Environment.NewLine, lines);
                syntaxTree = SyntaxTree.Parse(text);
            }
            else
            {
                syntaxTree = (SyntaxTree) state;
            }

            var lineSpan = syntaxTree.Text.Lines[lineIndex].Span;
            var classifiedSpans = Classifier.Classify(syntaxTree, lineSpan);
            
            foreach (var classifiedSpan in classifiedSpans)
            {
                var classifiedText = syntaxTree.Text.ToString(classifiedSpan.Span);

                switch (classifiedSpan.Classification)
                {
                    case Classification.Keyword:
                        Console.ForegroundColor = ConsoleColor.Blue;
                        break;
                    case Classification.Identifier:
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        break;
                    case Classification.Number:
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        break;
                    case Classification.Comment:
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        break;
                    case Classification.String:
                        Console.ForegroundColor = ConsoleColor.Green;
                        break;
                    case Classification.Text:
                    default:
                        Console.ForegroundColor = ConsoleColor.White;
                        break;
                }
                
                Console.Write(classifiedText);
                Console.ResetColor();
            }

            return syntaxTree;
        }

        [MetaCommand("showTree", "Shows the parse tree")]
        private void EvaluateShowTree()
        {
            _showTree = !_showTree;
            Console.WriteLine(_showTree ? "Showing parse trees." : "Not showing parse trees");
        }
        
        [MetaCommand("showProgram", "Shows the bound tree")]
        private void EvaluateShowProgram()
        {
            _showProgram = !_showProgram;
            Console.WriteLine(_showProgram ? "Showing bound tree." : "Not showing bound tree.");
        }

        [MetaCommand("cls", "Clears the screen")]
        private void EvaluateCls()
        {
            Console.Clear();
        }

        [MetaCommand("reset", "Clears all previous submissions")]
        private void EvaluateReset()
        {
            _previous = null;
            _variables.Clear();
            ClearSubmissions();
        }
        
        [MetaCommand("exit", "Exits the program")]
        private void EvaluateExit()
        {
            Environment.Exit(1);
        }
        
        [MetaCommand("load", "Loads a script file")]
        private void EvaluateLoad(string path)
        {
            path = Path.GetFullPath(path);
            
            if (!File.Exists(path))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"error: file does not exist '{path}'");
                Console.ResetColor();
                return;
            }

            var text = File.ReadAllText(path);
            EvaluateSubmission(text);
        }
        
        [MetaCommand("ls", "Lists all symbols")]
        private void EvaluateLs()
        {
            var compilation = _previous ?? emptyCompilation;

            var symbols = compilation.GetSymbols().OrderBy(s => s.Kind).ThenBy(s => s.Name);
            
            foreach (var symbol in symbols)
            {
                symbol.WriteTo(Console.Out);
                Console.WriteLine();
            }
        }
        
        [MetaCommand("dump", "Shows bound tree of a given function")]
        private void EvaluateDump(string functionName)
        {
            var compilation = _previous ?? emptyCompilation;
            var symbol = compilation.GetSymbols().OfType<FunctionSymbol>().SingleOrDefault(f => f.Name == functionName);
            
            if (symbol == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"error: function '{functionName}' does not exist");
                Console.ResetColor();
                return;
            }
            compilation.EmitTree(symbol, Console.Out);
        }

        protected override bool IsCompleteSubmission(string text)
        {
            if (string.IsNullOrEmpty(text))
                return true;

            var lastTwoLinesAreBlank = text.Split(Environment.NewLine)
                                            .Reverse()
                                            .TakeWhile(s => string.IsNullOrEmpty(s))
                                            .Take(2)
                                            .Count() == 2;

            if (lastTwoLinesAreBlank)
                return true;
            
            var syntaxTree = SyntaxTree.Parse(text);

            var lastMember = syntaxTree.Root.Members.LastOrDefault();
            if (lastMember == null || lastMember.GetLastToken().IsMissing)
                return false;

            return true;
        }
        
        protected override void EvaluateSubmission(string text)
        {
            var syntaxTree = SyntaxTree.Parse(text);
            var compilation = Compilation.CreateScript(_previous, syntaxTree);

            if (_showTree)
                syntaxTree.Root.WriteTo(Console.Out);

            if (_showProgram)
                compilation.EmitTree(Console.Out);

            var result = compilation.Evaluate(_variables);

            if (!result.Diagnostics.Any())
            {
                if (result.Value != null)
                {
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine(result.Value);
                    Console.ResetColor();
                }
                _previous = compilation;

                SaveSubmission(text);
            }
            else
            {
                Console.Out.WriteDiagnostics(result.Diagnostics);
            }
        }
        private static string GetSubmissionsDirectory()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var submissionsDirectory = Path.Combine(localAppData, "Vivian", "Submissions");
            return submissionsDirectory;
        }
        
        private void LoadSubmissions()
        {
            var submissionsDirectory = GetSubmissionsDirectory();
            if (!Directory.Exists(submissionsDirectory))
                return;
            
            var files = Directory.GetFiles(submissionsDirectory).OrderBy(f => f).ToArray();
            if (files.Length == 0)
                return;
            
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"Loaded {files.Length} submission(s)");
            Console.ResetColor();
            
            _loadingSubmissions = true;
            
            foreach (var file in files)
            {
                var text = File.ReadAllText(file);
                EvaluateSubmission(text);
            }

            _loadingSubmissions = false;
        }
        
        private static void ClearSubmissions()
        {
            var dir = GetSubmissionsDirectory();
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
        
        private static void SaveSubmission(string text)
        {
            if (_loadingSubmissions)
                return;
            
            var submissionsDirectory = GetSubmissionsDirectory();
            Directory.CreateDirectory(submissionsDirectory);

            var count = Directory.GetFiles(submissionsDirectory).Length;
            var name = $"submission{count:0000}";
            var fileName = Path.Combine(submissionsDirectory, name);
            File.WriteAllText(fileName, text);
        }
    }
}