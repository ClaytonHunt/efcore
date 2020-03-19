// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.EntityFrameworkCore.Tools
{
    internal interface IOperationExecutor : IDisposable
    {
        IDictionary AddMigration(string name, string outputDir, string contextType, string[] remainingArguments);
        IDictionary RemoveMigration(string contextType, bool force, string[] remainingArguments);
        IEnumerable<IDictionary> GetMigrations(string contextType, string[] remainingArguments);
        void DropDatabase(string contextType, string[] remainingArguments);
        IDictionary GetContextInfo(string name, string[] remainingArguments);
        void UpdateDatabase(string migration, string connectionString, string contextType, string[] remainingArguments);
        IEnumerable<IDictionary> GetContextTypes();

        IDictionary ScaffoldContext(
            string provider,
            string connectionString,
            string outputDir,
            string outputDbContextDir,
            string dbContextClassName,
            IEnumerable<string> schemaFilters,
            IEnumerable<string> tableFilters,
            bool useDataAnnotations,
            bool overwriteFiles,
            bool useDatabaseNames,
            string[] remainingArguments);

        string ScriptMigration(string fromMigration, string toMigration, bool idempotent, string contextType, string[] remainingArguments);

        string ScriptDbContext(string contextType, string[] remainingArguments);
    }
}
