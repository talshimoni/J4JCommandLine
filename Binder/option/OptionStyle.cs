﻿namespace J4JSoftware.CommandLine
{
    public enum OptionStyle
    {
        Undefined,
        Switch,
        SingleValued,
        // for non-collection properties which expect multiple values
        // to be parsed (e.g., flag enums)
        ConcatenatedSingleValue,
        Collection
    }
}