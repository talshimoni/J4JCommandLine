﻿using System.Linq;
using Xunit;

namespace J4JSoftware.Binder.Tests
{
    public class SingleProperties : BaseTest
    {
        [ Theory ]
        [ MemberData( nameof(TestDataSource.GetSinglePropertyData), MemberType = typeof(TestDataSource) ) ]
        public void Allocations( TestConfig config )
        {
            Initialize( config );
            
            Options!.CreateOptionsFromContextKeys( TestConfig!.OptionConfigurations);

            ValidateAllocations();
        }

        [ Theory ]
        [ MemberData( nameof(TestDataSource.GetSinglePropertyData), MemberType = typeof(TestDataSource) ) ]
        public void Parsing( TestConfig config )
        {
            Initialize( config );

            Options!.CreateOptionsFromContextKeys(TestConfig!.OptionConfigurations);

            ValidateConfiguration<BasicTarget>();
        }
    }
}