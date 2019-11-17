using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Diagnostics;
using static CourgetteTestAdapter.Logger;

// DO NOT UPDATE Nuget DEPENDENCIES TO LATEST VERSIONS, IT WILL CAUSE RUNTIME DLL DEPENDENCY ERRORS

// from nuget Microsoft.TestPlatform.ObjectModel
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace CourgetteTestAdapter
{
	public class MatchLocation
	{
		public int LineNumber;
		public string Value;
	}

	[Microsoft.VisualStudio.TestPlatform.ObjectModel.FileExtension(".ts")]
	[DefaultExecutorUri(TestExecutor.ExecutorUriString)]
	public class TestDiscoverer : ITestDiscoverer
	{
		// implementation of ITestDiscoverer.DiscoverTests()
		public void DiscoverTests(
			IEnumerable<string> sources,            // collection of test containers
			IDiscoveryContext discoveryContext,
			IMessageLogger logger,
			ITestCaseDiscoverySink discoverySink)   // used to send test cases found back to Visual Studio
		{
			Log("DiscoverTests() called");

			// find all test cases and report to VS using discoverySink.SendTestCase()
			GetTestCaseList(sources, discoverySink);
		}

		private string GetFileExtension(string fullPathFilename)
		{
			// get double-dotted extension
			string filename = Path.GetFileName(fullPathFilename);
			int firstDot = filename.IndexOf('.');
			if (firstDot < 0)
				return "";

			string extension = filename.Substring(firstDot);
			return extension;
		}

		public IEnumerable<TestCase> GetTestCaseList(IEnumerable<string> filenames, ITestCaseDiscoverySink discoverySink = null)
		{
			Log("GetTestCaseList() called");

			var testCases = new List<TestCase>();
			foreach (var filename in filenames)
			{
				string extension = GetFileExtension(filename);
				switch (extension)
				{
					case ".spec.ts":
						List<TestCase> specTestCases = GetSpecTestMethods(filename, discoverySink);
						testCases.AddRange(specTestCases);
						break;

					case ".courgette.ts":
						List<TestCase> courgetteTestCases = GetCourgetteTestMethods(filename, discoverySink);
						testCases.AddRange(courgetteTestCases);
						break;

					// TODO: add parsing for gherkin style tests
					//case ".gherkin.ts":
					//	List<TestCase> gherkinTestCases = GetGherkinTestMethods(filename, discoverySink);
					//	testCases.AddRange(gherkinTestCases);
					//	break;

					default:
						Log($"File extension {extension} was encountered, but no file parser was found for this file type");
						break;
				}
			}

			return testCases;
		}

		private string ReplaceDots(string name)
		{
			string nameWithoutDots = name.Replace('.', '\uFE52');	// replace full-stop with small full-stop
			return nameWithoutDots;
		}

		private string GetSuiteOrTestName(string token)
		{
			int leftBracket = token.IndexOf('(');
			string suiteOrTestName = token.Substring(leftBracket + 2);
			return suiteOrTestName;
		}

		private string RemoveLastPartOfPath(string fqnPath)
		{
			int leftBracket = fqnPath.LastIndexOf('.');
			if (leftBracket > 0)
				fqnPath = fqnPath.Remove(leftBracket);

			return fqnPath;
		}

		private List<TestCase> GetSpecTestMethods(string testFilename, ITestCaseDiscoverySink discoverySink)
		{
			//Debugger.Break();
			List<TestCase> testCases = new List<TestCase>();

			string fqTestName = Path.GetFileNameWithoutExtension(testFilename);
			if (fqTestName.Contains("."))
				fqTestName = Path.GetFileNameWithoutExtension(fqTestName);

			var matchLocations = GetSpecTestNames(testFilename);
			foreach (MatchLocation testText in matchLocations)
			{
				string token = testText.Value;
				if (token.StartsWith("describe(") || token.StartsWith("it("))
				{
					string suiteOrTestName = GetSuiteOrTestName(token);
					fqTestName += '.' + ReplaceDots(suiteOrTestName);
				}

				if (testText.Value == "});")
					fqTestName = RemoveLastPartOfPath(fqTestName);

				// only it() functions add a TestCase, skip everything else
				if (!token.StartsWith("it("))
					continue;

				// see https://github.com/microsoft/vstest-docs/blob/master/RFCs/0017-Managed-TestCase-Properties.md
				// TestCase.FullyQualifiedName will accept dot notation to indicate test hierarchy
				var testCase = new TestCase(fqTestName, TestExecutor.ExecutorUri, testFilename);
				testCase.DisplayName = GetSuiteOrTestName(token);
				testCase.CodeFilePath = testFilename;
				testCase.LineNumber = testText.LineNumber;
				testCases.Add(testCase);
				discoverySink?.SendTestCase(testCase);
			}

			return testCases;
		}

		private List<TestCase> GetCourgetteTestMethods(string filename, ITestCaseDiscoverySink discoverySink)
		{
			List<TestCase> testCases = new List<TestCase>();

			string fqnPath = Path.GetFileNameWithoutExtension(filename);
			if (fqnPath.Contains("."))
				fqnPath = Path.GetFileNameWithoutExtension(fqnPath);

			var matchLocations = GetCourgetteTestNames(filename);
			foreach (MatchLocation testText in matchLocations)
			{
				string token = testText.Value;
				if (token.StartsWith("@Suite("))
				{
					int leftBracket1 = token.IndexOf('(');
					fqnPath += '.' + token.Substring(leftBracket1 + 2);
					continue;
				}

				// only @Test() functions add a TestCase, skip everything else
				if (!token.StartsWith("@Test("))
					continue;

				int leftBracket2 = token.IndexOf('(');
				string testName = token.Substring(leftBracket2 + 2);

				string fullyQualifiedTestName = fqnPath + "." + testName;
				var testCase = new TestCase(fullyQualifiedTestName, TestExecutor.ExecutorUri, filename);
				testCase.DisplayName = testName;
				testCase.CodeFilePath = filename;
				testCase.LineNumber = testText.LineNumber;
				testCases.Add(testCase);
				discoverySink?.SendTestCase(testCase);
			}

			return testCases;
		}

		public int GetLineNumFromCharPos(string text, int charPos)
		{
			int lineNum = 1;
			for (int i = 0; i < charPos; i++)
			{
				if (text[i] == '\n')
					lineNum++;
			}
			return lineNum;
		}

		private IEnumerable<MatchLocation> GetSpecTestNames(string filename)
		{
			// open file and return list of all test cases found: 1 for each it('...') found
			string content = File.ReadAllText(filename);

			// look for:
			//	describe('...')
			//	it('...')
			//	});
			//
			// allow for either single or double quotes
			Regex regex = new Regex(@"describe\(['][^']+|describe\([""][^""]+|}\);|it\(['][^']+|it\([""][^""]+");
			MatchCollection matches = regex.Matches(content);

			return from Match match in matches
				select new MatchLocation { LineNumber = GetLineNumFromCharPos(content, match.Index), Value = match.Value };
		}

		private IEnumerable<MatchLocation> GetCourgetteTestNames(string filename)
		{
			// open file and return list of all test cases found: 1 for each it('...') found
			string content = File.ReadAllText(filename);

			// look for:
			//	@Suite('...')
			//	@Test('...')
			//
			// actual regex is: @Suite\(['"][^'"]+|@Test\(['"][^'"]+
			Regex regex = new Regex(@"@Suite\(['""][^'""]+|@Test\(['""][^'""]+");

			MatchCollection matches = regex.Matches(content);

			return from Match match in matches
				select new MatchLocation { LineNumber = GetLineNumFromCharPos(content, match.Index), Value = match.Value };
		}

	}
}
