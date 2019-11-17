using System;
using System.Collections.Generic;

// DO NOT UPDATE Nuget DEPENDENCIES TO LATEST VERSIONS, IT WILL CAUSE RUNTIME DLL DEPENDENCY ERRORS

// nuget Microsoft.TestPlatform.ObjectModel
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

// dll ref \Common7\IDE\CommonExtensions\Microsoft\TestWindow\Microsoft.VisualStudio.TestWindow.Interfaces.dll
using Microsoft.VisualStudio.TestWindow.Extensibility;
using Microsoft.VisualStudio.TestWindow.Extensibility.Model;

// ITestContainerDiscoverer & ITestContainer implementations required for unit tests that are not within dll/exe files, i.e. JavaScript
// requires UnitTestExtension entry in vsix manifest

namespace CourgetteTestAdapter
{
	// this class represents a single test container file (e.g. mytests.spec.js or mytests.gherkin.js)
	class TestContainer : ITestContainer
	{
		// non-interface fields
		private DateTime _timeStamp;
		//public string ParentProject;	// required for setting wwwroot when starting iisexpress

		// ITestContainer props
		public ITestContainerDiscoverer Discoverer { get; }
		public string Source { get; }
		public IEnumerable<Guid> DebugEngines => Array.Empty<Guid>();
		public FrameworkVersion TargetFramework => FrameworkVersion.None;
		public Architecture TargetPlatform => Architecture.Default;
		public bool IsAppContainerTestContainer => false;
		// ITestContainer methods
		public IDeploymentData DeployAppContainer() => null;
		public ITestContainer Snapshot() => new TestContainer(this);

		// ctor
		public TestContainer(ITestContainerDiscoverer discoverer, string testFilename, DateTime timeStamp)
		{
			this.Discoverer = discoverer;
			this.Source = testFilename;							// full path filename for test file (e.g. MyTests.spec.ts)
			this._timeStamp = timeStamp;
		}

		// copy ctor (required)
		private TestContainer(TestContainer copy) : this(copy.Discoverer, copy.Source, copy._timeStamp)
		{
		}

		// ITestContainer.CompareTo interface method
		public int CompareTo(ITestContainer other)
		{
			var container = other as TestContainer;
			if (container == null)
				return -1;

			var result = StringComparer.OrdinalIgnoreCase.Compare(this.Source, container.Source);
			if (result != 0)
				return result;

			return this._timeStamp.CompareTo(container._timeStamp);
		}

		// unsure when/if this gets invoked (not on ITestContainer interface)
		public override string ToString()
		{
			return this.Source + ":" + this.Discoverer.ExecutorUri.ToString();
		}
	}
}
