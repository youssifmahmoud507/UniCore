using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Text;

namespace UniCore.ArchitectureTests
{
    public class NoThrowTests
    {
        [Fact]
        public void No_source_file_contains_a_throw()
        {
            var root = FindSolutionRoot();
            if (root is null)
            {
                Assert.Fail("Could not locate the solution root (UniCore.slnx / .sln).");
                return;
            }

            var violations = new List<string>();

            foreach (var file in EnumerateSourceFiles(root))
            {
                var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file);

                foreach (var node in FindThrows(tree))
                {
                    var line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    violations.Add($"{Path.GetRelativePath(root, file)}:{line}");
                }
            }

            Assert.True(
                violations.Count == 0,
                "`throw` is forbidden (Rule A). Return Result.Fail(...) instead. Found at:\n" +
                string.Join("\n", violations));
        }

        [Theory]
        [InlineData("class A { void M() { throw new System.Exception(); } }")]
        [InlineData("class A { int M(object o) { return o is int i ? i : throw new System.Exception(); } }")]
        [InlineData("class A { int M(string? s) { return s?.Length ?? throw new System.Exception(); } }")]
        [InlineData("class A { void M() { try { } catch { throw; } } }")]
        public void Scanner_detects_throw_statements_and_expressions(string source)
        {
            var tree = CSharpSyntaxTree.ParseText(source);

            Assert.NotEmpty(FindThrows(tree));
        }

        [Fact]
        public void Scanner_ignores_code_without_throw()
        {
            var tree = CSharpSyntaxTree.ParseText("class A { int M() { return 1; } }");

            Assert.Empty(FindThrows(tree));
        }

        private static IEnumerable<SyntaxNode> FindThrows(SyntaxTree tree) =>
            tree.GetRoot().DescendantNodes().Where(n => n is ThrowStatementSyntax or ThrowExpressionSyntax);

        private static IEnumerable<string> EnumerateSourceFiles(string root) =>
            Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(p =>
                {
                    var normalized = p.Replace('\\', '/');
                    return !normalized.Contains("/bin/")
                        && !normalized.Contains("/obj/")
                        && !normalized.Contains("/.git/")
                        && !normalized.Contains("/.vs/");
                });

        private static string? FindSolutionRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);

            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "UniCore.slnx"))
                    || Directory.EnumerateFiles(dir.FullName, "*.sln").Any())
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }

            return null;
        }
    }
}
