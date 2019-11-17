using System;
using System.IO;
using System.Text.RegularExpressions;
using static CourgetteTestAdapter.Logger;

// nuget SourceMapToolkit
using SourcemapToolkit.SourcemapParser;

namespace CourgetteTestAdapter
{
	// 1-based row and column coords (SourcemapToolkit SourcePosition uses zero-based coords)
	public struct SourcePos
	{
		public int Line;
		public int Column;

		// when not found, return line 1, column 1
		public static SourcePos Notfound = new SourcePos { Line = 1, Column = 1 };

		// ctor that converts zero-based SourcePosition class to one-based SourcePos struct
		public SourcePos(SourcePosition zeroBasedPostion)
		{
			this.Line = zeroBasedPostion.ZeroBasedLineNumber + 1;
			this.Column = zeroBasedPostion.ZeroBasedColumnNumber + 1;
		}

		// converts one-based SourcePos struct to zero-based SourcePosition class
		public SourcePosition GetZeroBasedPosition()
		{
			return new SourcePosition() { ZeroBasedLineNumber = this.Line - 1, ZeroBasedColumnNumber = this.Column - 1 };
		}

		// use map file to get TS source file pos from JS file pos
		private static SourcePos FindTypeScriptLineNumber(string soureMapFile, SourcePos jsOneBasedSourcePos)
		{
			// convert to zero-based coords
			SourcePosition jsZeroBasedSourcePos = jsOneBasedSourcePos.GetZeroBasedPosition();

			// Parse the source map from file
			SourceMapParser parser = new SourceMapParser();
			SourceMap sourceMap;
			using (FileStream stream = new FileStream(soureMapFile, FileMode.Open))
			{
				sourceMap = parser.ParseSourceMap(new StreamReader(stream));
			}

			// look for JS source position in map entries
			// if we can't find the exact location in the map file, we get a null result, so to safeguard against this happening, 
			// we'll look 10 chars either side of the specfied column, and take the closest match if we can't find an exact match
			// if that also fails, we return notFound = [0,0] = line 1, column 1
			// (column is not currently used, but required to get the mapped line number)
			MappingEntry[] mapEntry = new MappingEntry[3];
			for (int delta = 0; delta <= 10; delta++)
			{
				SourcePosition posBefore = jsZeroBasedSourcePos;
				posBefore.ZeroBasedColumnNumber -= delta;

				SourcePosition posAfter = jsZeroBasedSourcePos;
				posAfter.ZeroBasedColumnNumber += delta;

				mapEntry[0] = sourceMap.GetMappingEntryForGeneratedSourcePosition(jsZeroBasedSourcePos);
				mapEntry[1] = sourceMap.GetMappingEntryForGeneratedSourcePosition(posBefore);
				mapEntry[2] = sourceMap.GetMappingEntryForGeneratedSourcePosition(posAfter);

				if (mapEntry[0] != null || mapEntry[1] != null || mapEntry[2] != null)
					break;
			}

			MappingEntry tsMapEnt = null;
			for (int i = 0; i < 3; i++)
			{
				if (mapEntry[i] != null)
				{
					tsMapEnt = mapEntry[i];
					break;
				}
			}

			if (tsMapEnt == null)
			{
				Log($"Unable to map Javascript source pos to TypeScript source file, mapping file was: {soureMapFile}");
				return SourcePos.Notfound;
			}
			else
			{
				Log($"Successfully mapped Javascript source pos to TypeScript pos using mapping file: {soureMapFile}");
				return new SourcePos(tsMapEnt.OriginalSourcePosition);
			}
		}

		public static SourcePos GetErrorPosFromStackTrace(string fullPathFilename, string stackTrace)
		{
			if (fullPathFilename.EndsWith(".ts"))
				return GetTsErrorPosFromStackTrace(fullPathFilename, stackTrace);

			if (fullPathFilename.EndsWith(".js"))
				return GetJsErrorPosFromStackTrace(fullPathFilename, stackTrace);

			Log($"GetLineNumberFromStackTrace(): Unexpected file extension. Expected .js or .ts, filename was {fullPathFilename}");
			return SourcePos.Notfound;
		}

		// maps location of error in SpecTest.stackTrace from Javascript to TypeScript source file
		private static SourcePos GetTsErrorPosFromStackTrace(string tsFullPathFilename, string stackTrace)
		{
			Log($"Entered GetTsErrorPosFromStackTrace(), tsFullPathFilename = '{tsFullPathFilename}'");

			// first get the location of the error in the Javascript source file from the stack trace
			string jsFullPathFilename = tsFullPathFilename.Replace(".ts", ".js");
			SourcePos jsPos = GetJsErrorPosFromStackTrace(jsFullPathFilename, stackTrace);

			// check we have a map file available to translate to the the TypeScript source file location
			string mapFilename = $"{jsFullPathFilename}.map";
			if (!File.Exists(mapFilename))
			{
				Log($"Map file {mapFilename} could not be found, cannot translate Javascript error location to TypeScript source location - try rebuilding project to regenerate map file");
				return SourcePos.Notfound;
			}

			// do the translation
			SourcePos tsOneBasedPos = FindTypeScriptLineNumber(mapFilename, jsPos);
			Log($"Mapped Javscript error location to TypeScript error location: line={tsOneBasedPos.Line}, column={tsOneBasedPos.Column}");

			return tsOneBasedPos;
		}

		// extracts location of Javascript error in SpecTest.stackTrace
		private static SourcePos GetJsErrorPosFromStackTrace(string jsFullPathFilename, string stackTrace)
		{
			Log($"Entered GetJsErrorPosFromStackTrace(), tsFullPathFilename = '{jsFullPathFilename}'");

			// first extract JS source file error location from stack trace
			string jsFilename = Path.GetFileName(jsFullPathFilename);
			string pattern = $"(?<={jsFilename}:)[0-9]+:[0-9]+";  // technically the filename contains '.' chars which should be escaped in regex, but this will still work as '.' matches any char
			Regex regex = new Regex(pattern);
			var match = regex.Match(stackTrace);
			if (!match.Success)
			{
				Log($"Unable to find JS source file '{jsFilename}' in stack dump {stackTrace}");
				return SourcePos.Notfound;
			}

			// should now have <line>:<col> in format nnn:nnn
			int colon = match.Value.IndexOf(':');
			string jsLineStr = match.Value.Substring(0, colon);
			string jsColStr = match.Value.Substring(colon + 1);

			SourcePos jsPos = new SourcePos();
			jsPos.Line = Int32.TryParse(jsLineStr, out jsPos.Line) ? jsPos.Line : 1;
			jsPos.Column = Int32.TryParse(jsColStr, out jsPos.Column) ? jsPos.Column : 1;
			Log($"Found JS source file '{jsFilename}' in stack dump, Javascript error location line={jsPos.Line}, column={jsPos.Column}");

			return jsPos;
		}

	}
}
