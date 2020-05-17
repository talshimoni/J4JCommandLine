﻿using System.Collections.Generic;
using System.Reflection;

namespace J4JSoftware.CommandLine
{
    public interface IParseResult
    {
        string Key { get; }
        int NumParameters { get; }
        List<string> Parameters { get; }
    }
}