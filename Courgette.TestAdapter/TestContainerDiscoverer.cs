using System;
using System.IO;
using System.Collections.Generic;
using Debugger = System.Diagnostics.Debugger;
using static CourgetteTestAdapter.Logger;

// DO NOT UPDATE Nuget DEPENDENCIES TO LATEST VERSIONS, IT WILL CAUSE RUNTIME DLL DEPENDENCY ERRORS

// nuget EnvDTE
using EnvDTE;

// nuget System.ComponentModel.Composition
using System.ComponentModel.Composition;

// dll ref \Common7\IDE\CommonExtensions\Microsoft\TestWindow\Microsoft.VisualStudio.TestWindow.Interfaces.dll
using Microsoft.VisualStudio.TestWindow.Extensibility;

// dll ref \VSSDK\VisualStudioIntegration\Tools\Bin\lib\Microsoft.VisualStudio.Shell.Framework.dll
using Microsoft.VisualStudio.Shell;

// dll ref \VSSDK\VisualStudioIntegration\Common\Assemblies\v2.0\Microsoft.VisualStudio.Shell.Interop.dll
using Microsoft.VisualStudio.Shell.Interop;

// see the following for an overview:
//	https://github.com/etas/vs-boost-unit-test-adapter/wiki/Visual-Studio-Test-Platform-Primer
//
// and see the following for a more detailed test adapter requirements:
//	https://github.com/microsoft/vstest-docs/blob/master/RFCs/0004-Adapter-Extensibility.md
//	https://docs.microsoft.com/en-us/archive/msdn-magazine/2017/august/visual-studio-creating-extensions-for-multiple-visual-studio-versions
//
// and the following for debugging tips:
//	https://bideveloperextensions.github.io/features/VSIXextensionmodel/
//

// ITestContainerDiscoverer & ITestContainer implementations required for unit tests that are not within dll/exe files, i.e. TypeScript/JavaScript
// requires UnitTestExtension entry in vsix manifest

namespace CourgetteTestAdapter
{
	// this class finds all the following test test container files:
	//	*.spec.ts (*.spec.js not currently searched for)
	//	*.courgette.ts
	//	*.gherkin.ts
	[Export(typeof(ITestContainerDiscoverer))]
	class TestContainerDiscoverer : ITestContainerDiscoverer
	{
		List<TestContainer> testContainers = null;
		public event EventHandler TestContainersUpdated;	// this should be raised if we change the test containers (due to detected file watcher change)
		private IServiceProvider serviceProvider;
		public Uri ExecutorUri => TestExecutor.ExecutorUri;

		// ctor
		[ImportingConstructor]
		public TestContainerDiscoverer([Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider)
		{
			Log("TestContainerDiscoverer() ctor called");

			this.serviceProvider = serviceProvider;

			// https://github.com/getgauge/gauge-visualstudio/blob/master/Gauge.VisualStudio.TestAdapter/TestContainerDiscoverer.cs
			//var events2 = (Events2)dte.Events;
			//_projectItemsEvents = events2.ProjectItemsEvents;

			//_projectItemsEvents.ItemAdded += UpdateTestContainersIfGaugeSpecFile;
			//_projectItemsEvents.ItemRemoved += UpdateTestContainersIfGaugeSpecFile;
			//_projectItemsEvents.ItemRenamed += (item, s) => UpdateTestContainersIfGaugeSpecFile(item);
		}

		public IEnumerable<ITestContainer> TestContainers {
			get
			{
				Log("get TestContainers called");
				if (testContainers == null)
				{
					FindAllTestContainers();
					return testContainers;
				}
				else
					return testContainers;
			}
		}

		private void CheckIfTestContainerAndAdd(string projectFilename, string filename, string[] extensions)
		{
			foreach (string extension in extensions)
			{
				if (filename.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
				{
					//Console.Error.WriteLine($"Found test container, filename={filename}");
					Log($"Found test container, project={projectFilename}, filename={filename}");
					var container = new TestContainer(this, filename, DateTime.Now);
					this.testContainers.Add(container);
				}
			}
		}

		// find all projects in current solution, and for each project, find all files
		private void FindAllTestContainers()
		{
			// you need to modify FileExtension attributes in TestDiscoverer class if you change the following:
			var extensions = new string[] { ".spec.ts", ".courgette.ts", ".gherkin.ts" };


			Log($"FindAllTestContainers() called, searching for files in solution with extensions {String.Join(", ", extensions)}");

			// empty container list and refill
			this.testContainers = new List<TestContainer>();

			DTE dte = serviceProvider.GetService(typeof(DTE)) as DTE;
			foreach (Project project in dte.Solution.Projects)
			{
				// skip this project, as it leads to access violations when trying to extract full path filename
				if (project.Name == "Miscellaneous Files")
					continue;

				foreach (ProjectItem item in project.ProjectItems)
				{
					string fullPathFilename;
					string name = item.Name;
					//Log($"Trying to get full path for file {name}");
					try
					{
						// appears to be a bug in DTE COM interfaces whereby FileCount length is 1, but actual filename is stored in array element [1] not [0]
						// this throws an access violation when the project name is "Miscellaneous Files", so skip this project, but leave exception handling in
						fullPathFilename = item.Properties.Item("FullPath").Value;
					}
					catch (Exception ex)
					{
						Log($"FindAllTestContainers(): Exception thrown: {ex.ToString()}");
						continue;
					}

					if (fullPathFilename.EndsWith("\\"))
					{
						string[] filenames = Directory.GetFiles(fullPathFilename, "*", SearchOption.AllDirectories);
						foreach (string filename in filenames)
							CheckIfTestContainerAndAdd(project.FileName, filename, extensions);
					}
					else
					{
						// not directory, so is filename
						CheckIfTestContainerAndAdd(project.FileName, fullPathFilename, extensions);
					}
				}
			}

			Log($"FindAllTestContainers() completed, managed to find {this.testContainers.Count} files in solution with extensions {String.Join(", ", extensions)}");
		}

		// we should raise this event if we update the test containers (due to user file changes) so that VS can update tests
		// currently we have no file watchers implemented, so this isn't used yet
		protected virtual void OnTestContainersUpdated(EventArgs e)
		{
			Log($"OnTestContainersUpdated() called - file changes detected");

			// execute delgate chain for all attached handlers
			TestContainersUpdated?.Invoke(this, e);
		}
	}
}
