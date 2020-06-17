﻿using System.Collections.Generic;

namespace J4JSoftware.CommandLine
{
    public interface IConsoleOutput
    {
        void Initialize();
        void AddLine( ConsoleSection section, string? text = null );
        void AddError( CommandLineLogger.ConsolidatedLog consolidatedLog );
        void AddOption( List<string> keys, string? description = null, string? defaultText = null );
        void Display();
    }
}
