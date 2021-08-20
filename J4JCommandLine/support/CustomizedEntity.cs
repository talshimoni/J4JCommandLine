﻿using System;
using System.Reflection;

namespace J4JSoftware.Configuration.CommandLine
{
    public class CustomizedEntity : ICustomized
    {
        protected CustomizedEntity(
            bool allowNoOperatingSystem
        )
        {
            var attr = GetType().GetCustomAttribute<CommandLineCustomizationAttribute>();
            var attrOS = GetType().GetCustomAttribute<CommandLineOperatingSystemAttribute>();

            if (attrOS != null && attr != null)
            {
                OperatingSystem = attrOS.OperatingSystem;
                Customization = attr.Customization;
                Priority = attr.Priority;
                TextComparison = attrOS.TextComparison;
            }
            else
            {
                if( attr == null || !allowNoOperatingSystem )
                {
                    OperatingSystem = OSNames.Undefined;
                    Customization = Customization.Invalid;
                    Priority = int.MinValue;
                }
                else
                {
                    OperatingSystem = OSNames.Universal;
                    Customization = attr.Customization;
                    Priority = attr.Priority;
                }

                TextComparison = attrOS?.TextComparison ?? StringComparison.OrdinalIgnoreCase;
            }
        }

        public string OperatingSystem { get; }
        public Customization Customization { get; }
        public int Priority { get; }
        public StringComparison TextComparison { get; }
    }
}