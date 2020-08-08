using System;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using static CourgetteTestAdapter.Logger;

// DO NOT UPDATE Nuget DEPENDENCIES TO LATEST VERSIONS, IT WILL CAUSE RUNTIME DLL DEPENDENCY ERRORS

// from nuget Microsoft.TestPlatform.ObjectModel
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

// note if you are having problems debugging the TestExecutor class (process unexpectedly exits)
// then ensure you have a .runsettings unit test config file (within the project with the unit tests)
// and in the config file set the <TestSessionTimeout> parameter (milliseconds) to 10 mins or so:
//
// <TestSessionTimeout>600000</TestSessionTimeout>
//
namespace CourgetteTestAdapter
{
	[ExtensionUri(TestExecutor.ExecutorUriString)]
	class TestExecutor : ITestExecutor
	{
		private bool _cancelled;
		//private IServiceProvider _serviceProvider = null;
		public const string ExecutorUriString = "executor://CourgetteTestExecutor";
		public static Uri ExecutorUri => new System.Uri(ExecutorUriString);

		// ctor injection (not an option for ITestExecutor implementation as it requires a parameterless constructor)
		//[ImportingConstructor]
		//public TestExecutor([Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider)
		//{
		//	_serviceProvider = serviceProvider;
		//}

		// property injection (not an option because we're not inside VS2017, we're in a separate testhost.x86 process)
		//[Import(typeof(SVsServiceProvider))]
		//public IServiceProvider ServiceProvider { get; set; }

		// to get IServiceProvider in ITestExecutor, the following might work:
		//	https://stackoverflow.com/questions/34354901/visual-studio-unittest-adapter-how-to-detect-active-configuration-in-executor?rq=1

		public void Cancel()
		{
			Log("Cancel() called");

			_cancelled = true;
		}

		TestCase FindVsTestResult(TsTestResult tsTestResult, IEnumerable<TestCase> vsTests)
		{
			//Log($"Looking for test {tsTestResult.displayName} from file {tsTestResult.testFilename} among the {vsTests.Count()} vsTests");

			foreach (TestCase vsTestCase in vsTests)
			{
				if (tsTestResult.testFilename == vsTestCase.Source && tsTestResult.displayName == vsTestCase.DisplayName)
					return vsTestCase;
			}

			// not found
			Log($"Unable to find test cases for test result, test result='{tsTestResult.displayName}' in file {tsTestResult.testFilename}");
			Log("VS Test case list is as follows:");
			foreach (TestCase vsTestCase in vsTests)
				Log($"filename={vsTestCase.Source} test name = {vsTestCase.DisplayName}");

			return null;
		}

		private TestResult ProcessTsTestResult(TsTestResult tsTestResult, TestCase vsTestCase)
		{
			Log("RunJavaScriptTests() called");

			// init result object
			TestResult vsResult = new TestResult(vsTestCase);
			vsResult.DisplayName = vsTestCase.DisplayName;
			vsResult.ComputerName = Environment.MachineName;
			vsResult.Outcome = tsTestResult.testOutcome;
			vsResult.StartTime = tsTestResult.startTime;
			vsResult.EndTime = tsTestResult.endTime;
			vsResult.Duration = new TimeSpan(tsTestResult.durationMs * 10000);

			switch (vsResult.Outcome)
			{
				case TestOutcome.Passed:
					vsResult.ErrorMessage = "Test passed successfully";
					break;

				case TestOutcome.Skipped:
					break; // do nothing

				case TestOutcome.None:
					Log($"TestOutcome.None for test {vsResult.TestCase.FullyQualifiedName}");
					break;

				case TestOutcome.NotFound:
					Log($"TestOutcome.NotFound for test {vsResult.TestCase.FullyQualifiedName}");
					break;

				case TestOutcome.Failed:
					// test failed
					vsResult.ErrorMessage = tsTestResult.errorMessage;
					vsResult.ErrorStackTrace = tsTestResult.errorStackTrace;

					string fullFilename = vsResult.TestCase.Source;
					string filename = Path.GetFileName(fullFilename);

					// this method can handle both .js and .ts files
					int line = SourcePos.GetErrorPosFromStackTrace(fullFilename, tsTestResult.errorStackTrace).Line;

					// get function name from stack trace
					Regex regex = new Regex("[A-Za-z0-9]+");
					Match match = regex.Match(vsResult.ErrorStackTrace.Substring(7));
					string function = match.Value;

					// thank-you denvercoder9 (aka Moe Seth)
					// https://social.msdn.microsoft.com/Forums/vstudio/en-US/3be3175d-dce1-48bc-9ffe-10627d99962c/questions-to-unit-test-explorer-gurus-errorstacktrace?forum=vstest
					// "at testMethod1 in C:\TestingProject\UnitTest1\UnitTest1\test.js:line 9"
					vsResult.ErrorStackTrace = $"at {function}() in {fullFilename}:line {line}";
					break;
			}

			Log("...finished checking test results for requested test");

			return vsResult;
		}

		private void ProcessTestFileResults(string testFilename, List<TsTestResult> testFileResults, IEnumerable<TestCase> vsTests, IFrameworkHandle frameworkHandle)
		{
			foreach (TsTestResult tsTestResult in testFileResults)
			{
				if (_cancelled)
					break;

				TestCase vsTestCase = FindVsTestResult(tsTestResult, vsTests);
				if (vsTestCase == null)
					continue;	// not found, so skip

				//frameworkHandle.SendMessage(TestMessageLevel.Informational, "Starting external test for " + vsTestCase.DisplayName);
				//frameworkHandle.RecordStart(vsTestCase);

				TestResult testOutcome = ProcessTsTestResult(tsTestResult, vsTestCase);

				//frameworkHandle.RecordEnd(vsTestCase, testOutcome);
				frameworkHandle.RecordResult(testOutcome);
				//frameworkHandle.SendMessage(TestMessageLevel.Informational, "Test result: " + testOutcome.Outcome.ToString());
			}
		}

		private async Task<List<TsTestResult>> GetBrowserResultsAsync(VsHttpServer listener)
		{
			//Debugger.Break();

			// receive results from chrome via http PUT
			Log("GetBrowserResultsAsync(): awaiting browser response...");
			string json = await listener.GetResponseFromBrowserAsync().ConfigureAwait(false);
			Log("GetBrowserResultsAsync(): ...json response received");
			Log($"json = {json}");

			// deserialise json test results
			Log("GetBrowserResultsAsync(): about to deserialise...");
			List<TsTestResult> browserResults = new List<TsTestResult>();
			try
			{
				browserResults = Json.Deserialize<List<TsTestResult>>(json);
				Log("GetBrowserResultsAsync(): ...json deserialised");
			}
			catch (Exception ex)
			{
				Log($"GetBrowserResultsAsync(): ...json deserialisation {ex.GetType().Name} thrown: {ex.Message}");
			}

			return browserResults;
		}

		// TODO: Check if this info can be replaced by IRunContext.SolutionDirectory / IRunContext.TestRunDirectory
		private string GetParentProjectFolder(string fullPathFilename)
		{
			string folder = Path.GetDirectoryName(fullPathFilename);
			string[] projectFiles = null;
			do
			{
				projectFiles = Directory.GetFiles(folder, "*.csproj");
				if (projectFiles.Length == 0)
					folder = folder = Directory.GetParent(folder).FullName;
			} while (projectFiles.Length == 0 && folder.Length > 3);

			return folder;
		}

		private string GetIISConfigFile(string fullPathFilename)
		{
			string folder = Path.GetDirectoryName(fullPathFilename);
			string[] projectFiles = null;
			do
			{
				projectFiles = Directory.GetFiles(folder, "*.sln");
				if (projectFiles.Length == 0)
					folder = folder = Directory.GetParent(folder).FullName;
			} while (projectFiles.Length == 0 && folder.Length > 3);

			// folder contains folder for .sln file
			string iisConfigFile = $"{folder}\\.vs\\config\\applicationhost.config";

			return iisConfigFile;
		}

		// async test runner, called once for each test container/file
		private async Task RunTestFileAsync(IRunContext runContext, VsHttpServer listener, string issConfig, string testFilename, IEnumerable<TestCase> selectedTests, IFrameworkHandle frameworkHandle)
		{
			Log($"Entered RunTestFileAsync(), testFilename={testFilename}");

			// TODO: perhaps add wwwroot path in .runsettings file?
			string wwwRoot = GetParentProjectFolder(testFilename).Replace('\\','/');
			string relativeFilePath = testFilename.Substring(wwwRoot.Length).Replace('\\', '/');

			// get start time
			var startTime = DateTime.Now;

			// start browser & run the tests, then get the results back from browser
			var browser = new Browser(runContext, wwwRoot, relativeFilePath);
			await browser.LaunchAsync().ConfigureAwait(false);
			List<TsTestResult> testFileResults = await GetBrowserResultsAsync(listener).ConfigureAwait(false);

			// calculate total time to run tests + get results, and also the average time of each test
			//browserResults.TestStartTime = startTime;
			//browserResults.TestEndTime = DateTime.Now;
			//int numTests = (from test in tests where test.Source == testFilename select test).Count();
			//browserResults.Duration = new TimeSpan((browserResults.TestEndTime - browserResults.TestStartTime).Ticks / numTests);

			// process and return test results back to Visual Studio
			ProcessTestFileResults(testFilename, testFileResults, selectedTests, frameworkHandle);

			// close the headless browser
			Log($"RunTestsAsync(): Attempting to close browser...");
			await browser.CloseAsync().ConfigureAwait(false);
			Log($"RunTestsAsync(): ...browser closed");
		}

		// called by VS when user wants to run selection of tests
		public void RunTests(IEnumerable<TestCase> selectedTests, IRunContext runContext, IFrameworkHandle frameworkHandle)
		{
			try
			{
				Log("RunTests(IEnumerable<TestCase> ...) called");

				// because multiple tests may be within the same source file, or scattered across several, we need to
				// optimise things so that we only fire up the browser once for each separate test container file found

				// switch to C# 8.0/VS2019 and use using shorthand syntax: var listener = new VsHttpServer(8638); (no trailing braces required)
				_cancelled = false;
				using (var listener = new VsHttpServer(8638)) // ensure the listener is started first
				{
					// get list of distinct filenames for requested tests
					IEnumerable<string> testFilenames = (from TestCase test in selectedTests select test.Source).Distinct();

					// find IIS config file for parent solution of tests, for now assume tests are all in single solution (this will need to be changed)
					string iisConfig = GetIISConfigFile(testFilenames.First());

					// launch IIS Express (if not already running)
					Browser.LaunchIISExpress(iisConfig);

					// Launch all tests asynchronously (should really limit concurrency with a SemaphoreSlim to 16 browsers or so in case someone has 50+ test containers).
					// Unless there are a large number of long-running tests, it will be faster to use a single browser and fire all the test files at it sequentially
					// (browser startup and process exit takes a long time, ~6 seconds, executing a page of tests is likely to be < 50-100ms)
					var tasks = testFilenames.Select(filename => RunTestFileAsync(runContext, listener, iisConfig, filename, selectedTests, frameworkHandle));

					// and then thunk back to synchronous world using blocking wait-for-all
					// we need to block here to ensure all the browsers that were started have been closed
					Task.WhenAll(tasks).GetAwaiter().GetResult();

					Log("RunTests(IEnumerable<TestCase> ...) all tasks completed");
				}
			}
			catch (Exception ex)
			{
				string errMsg = $"RunTests(IEnumerable<TestCase> ...): Exception thrown , exception={ex.ToString()}";
				Log(errMsg);
				Console.Error.WriteLine(errMsg);
			}
		}

		// called by VS when user wants to run all tests
		public void RunTests(IEnumerable<string> sources, IRunContext runContext, IFrameworkHandle frameworkHandle)
		{
			try
			{
				Log("RunTests(IEnumerable<string> ...) called");
				Log($"SolutionDirectory is {runContext.SolutionDirectory}, TestRunDirectory is {runContext.TestRunDirectory}");

				// inform user of all test files found
				frameworkHandle.SendMessage(TestMessageLevel.Informational, "Running from process: " + Process.GetCurrentProcess() + " ID:" + Process.GetCurrentProcess().Id.ToString());
				foreach (string filename in sources)
					frameworkHandle.SendMessage(TestMessageLevel.Informational, "Finding tests in source: " + filename);

				// now find and report all of the tests inside each of those files
				var testDisco = new TestDiscoverer();
				IEnumerable<TestCase> allTests = testDisco.GetTestCaseList(sources);
				foreach (var test in allTests)
					frameworkHandle.SendMessage(TestMessageLevel.Informational, "Found test: " + test.DisplayName);

				// call RunTests(IEnumerable<TestCase> ...) to execute tests
				RunTests(allTests, runContext, frameworkHandle);
			}
			catch (Exception ex)
			{
				string errMsg = $"RunTests(IEnumerable<string> ...): Exception thrown , testFilename={ex.ToString()}";
				Log(errMsg);
				Console.Error.WriteLine(errMsg);
			}
		}

	}
}
