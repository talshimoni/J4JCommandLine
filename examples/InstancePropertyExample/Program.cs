﻿using System;
using System.Linq;
using J4JSoftware.Configuration.CommandLine;
using J4JSoftware.Configuration.J4JCommandLine;
using Microsoft.Extensions.Configuration;

namespace J4JSoftware.CommandLine.Examples
{
    class Program
    {
        static void Main(string[] args)
        {
            var displayHelp = CompositionRoot.Default.DisplayHelp;

            var config = new ConfigurationBuilder()
                .AddJ4JCommandLine( OSNames.Windows, CompositionRoot.Default.Host!.Services, out var options)
                .Build();

            if (options == null)
                throw new NullReferenceException(nameof(options));

            var intValue = options.Bind<Configuration, int>(x => x.IntValue, "i")!
                .SetDefaultValue(75)
                .SetDescription("An integer value");

            var textValue = options.Bind<Configuration, string>(x => x.TextValue, "t")!
                .SetDefaultValue("a cool default")
                .SetDescription("A string value");

            options.DisplayHelp(displayHelp);
            Console.WriteLine("\n===============\n");
            options.DisplayHelp(new DisplayColorHelp(null));

            var parsed = config.Get<Configuration>();

            if (parsed == null)
            {
                Console.WriteLine("Parsing failed");

                Environment.ExitCode = -1;
                return;
            }

            Console.WriteLine("Parsing succeeded");

            Console.WriteLine($"IntValue is {parsed.IntValue}");
            Console.WriteLine($"TextValue is {parsed.TextValue}");

            Console.WriteLine(options.UnkeyedValues.Count == 0
                ? "No unkeyed parameters"
                : $"Unkeyed parameters: {string.Join(", ", options.UnkeyedValues)}");
        }
    }
}
