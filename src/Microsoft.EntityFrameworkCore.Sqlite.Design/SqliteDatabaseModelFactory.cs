// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Scaffolding.Internal;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using Microsoft.EntityFrameworkCore.Utilities;
using Microsoft.Extensions.Logging;

namespace Microsoft.EntityFrameworkCore.Scaffolding
{
    public class SqliteDatabaseModelFactory : IDatabaseModelFactory
    {
        public SqliteDatabaseModelFactory([NotNull] ILoggerFactory loggerFactory)
        {
            Check.NotNull(loggerFactory, nameof(loggerFactory));

            Logger = loggerFactory.CreateCommandsLogger();
        }

        public virtual ILogger Logger { get; }

        private SqliteConnection _connection;
        private TableSelectionSet _tableSelectionSet;
        private DatabaseModel _databaseModel;
        private Dictionary<string, TableModel> _tables;
        private Dictionary<string, ColumnModel> _tableColumns;

        private static string ColumnKey(TableModel table, string columnName)
            => "[" + table.Name + "].[" + columnName + "]";

        private void ResetState()
        {
            _connection = null;
            _tableSelectionSet = null;
            _databaseModel = new DatabaseModel();
            _tables = new Dictionary<string, TableModel>(StringComparer.OrdinalIgnoreCase);
            _tableColumns = new Dictionary<string, ColumnModel>(StringComparer.OrdinalIgnoreCase);
        }

        public virtual DatabaseModel Create(string connectionString, TableSelectionSet tableSelectionSet)
        {
            Check.NotEmpty(connectionString, nameof(connectionString));
            Check.NotNull(tableSelectionSet, nameof(tableSelectionSet));

            ResetState();

            using (_connection = new SqliteConnection(connectionString))
            {
                _connection.Open();
                _tableSelectionSet = tableSelectionSet;

                string databaseName = null;
                try
                {
                    databaseName = Path.GetFileNameWithoutExtension(_connection.DataSource);
                }
                catch (ArgumentException)
                {
                    // graceful fallback
                }

                _databaseModel.DatabaseName = !string.IsNullOrEmpty(databaseName)
                    ? databaseName
                    : _connection.DataSource;

                GetSqliteMaster();
                GetColumns();
                GetIndexes();
                GetForeignKeys();
                return _databaseModel;
            }
        }

        private void GetSqliteMaster()
        {
            using (var command = _connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT name FROM sqlite_master" +
                    " WHERE type = 'table' AND name <> 'sqlite_sequence'";

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var name = reader.GetValueOrDefault<string>("name");

                        Logger.LogTrace(SqliteDesignStrings.FoundTable(name));

                        if (_tableSelectionSet.Allows(name))
                        {
                            var table = new TableModel
                            {
                                Database = _databaseModel,
                                Name = name
                            };

                            _databaseModel.Tables.Add(table);
                            _tables.Add(name, table);
                        }
                        else
                        {
                            Logger.LogTrace(SqliteDesignStrings.TableNotInSelectionSet(name));
                        }
                    }
                }
            }

            using (var command = _connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT name, tbl_name FROM sqlite_master WHERE type = 'index'";

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var indexName = reader.GetValueOrDefault<string>("name");
                        var tableName = reader.GetValueOrDefault<string>("tbl_name");

                        Logger.LogTrace(SqliteDesignStrings.FoundIndex(indexName, tableName));

                        if (_tables.ContainsKey(tableName))
                        {
                            var table = _tables[tableName];

                            table.Indexes.Add(new IndexModel
                            {
                                Name = indexName,
                                Table = table
                            });
                        }
                        else
                        {
                            Logger.LogTrace(SqliteDesignStrings.IndexTableNotInSelectionSet(indexName, tableName));
                        }
                    }
                }
            }
        }

        private void GetColumns()
        {
            foreach (var table in _databaseModel.Tables)
            {
                using (var command = _connection.CreateCommand())
                {
                    command.CommandText = $"PRAGMA table_info(\"{table.Name.Replace("\"", "\"\"")}\");";

                    using (var reader = command.ExecuteReader())
                    {
                        var ordinal = 0;
                        while (reader.Read())
                        {
                            var columnName = reader.GetValueOrDefault<string>("name");
                            var dataType = reader.GetValueOrDefault<string>("type");
                            var primaryKeyOrdinal = reader.GetValueOrDefault<int>("pk");
                            var notNull = reader.GetValueOrDefault<bool>("notnull");
                            var defaultValue = reader.GetValueOrDefault<string>("dflt_value");

                            Logger.LogTrace(SqliteDesignStrings.FoundColumn(
                                table.Name, columnName, dataType, ordinal,
                                notNull, primaryKeyOrdinal, defaultValue));

                            var column = new ColumnModel
                            {
                                Table = table,
                                Name = columnName,
                                DataType = dataType,
                                Ordinal = ordinal++,
                                IsNullable = !notNull && (primaryKeyOrdinal == 0),
                                PrimaryKeyOrdinal = primaryKeyOrdinal == 0 ? default(int?) : primaryKeyOrdinal,
                                DefaultValue = defaultValue
                            };

                            table.Columns.Add(column);
                            _tableColumns[ColumnKey(table, column.Name)] = column;
                        }
                    }
                }
            }
        }

        private void GetIndexes()
        {
            foreach (var table in _databaseModel.Tables)
            {
                using (var indexInfo = _connection.CreateCommand())
                {
                    indexInfo.CommandText = $"PRAGMA index_list(\"{table.Name.Replace("\"", "\"\"")}\");";

                    using (var reader = indexInfo.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var indexName = reader.GetValueOrDefault<string>("name");
                            var isUnique = reader.GetValueOrDefault<bool>("unique");
                            var index = table.Indexes.FirstOrDefault(i => i.Name.Equals(indexName, StringComparison.OrdinalIgnoreCase));

                            if (index != null)
                            {
                                index.IsUnique = isUnique;

                                Logger.LogTrace(SqliteDesignStrings.FoundUniqueIndex(indexName, table.Name));
                            }
                        }
                    }

                    foreach (var index in table.Indexes)
                    {
                        var indexColumns = _connection.CreateCommand();
                        indexColumns.CommandText = $"PRAGMA index_info(\"{index.Name.Replace("\"", "\"\"")}\");";

                        index.IndexColumns = new List<IndexColumnModel>();
                        using (var reader = indexColumns.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var columnName = reader.GetValueOrDefault<string>("name");
                                var indexOrdinal = reader.GetValueOrDefault<int>("seqno");

                                Logger.LogTrace(SqliteDesignStrings.FoundIndexColumn(
                                    index.Name, table.Name, columnName, indexOrdinal));

                                if (string.IsNullOrEmpty(columnName))
                                {
                                    Logger.LogWarning(SqliteDesignStrings
                                        .ColumnNameEmptyOnIndex(index.Name, table.Name));
                                    continue;
                                }

                                var column = _tableColumns[ColumnKey(index.Table, columnName)];

                                var indexColumn = new IndexColumnModel
                                {
                                    Ordinal = indexOrdinal,
                                    Column = column
                                };

                                index.IndexColumns.Add(indexColumn);
                            }
                        }
                    }
                }
            }
        }

        private void GetForeignKeys()
        {
            foreach (var dependentTable in _databaseModel.Tables)
            {
                using (var fkList = _connection.CreateCommand())
                {
                    fkList.CommandText = $"PRAGMA foreign_key_list(\"{dependentTable.Name.Replace("\"", "\"\"")}\");";

                    var tableForeignKeys = new Dictionary<int, ForeignKeyModel>();

                    using (var reader = fkList.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var id = reader.GetValueOrDefault<int>("id");
                            var principalTableName = reader.GetValueOrDefault<string>("table");
                            var fromColumnName = reader.GetValueOrDefault<string>("from");
                            var toColumnName = reader.GetValueOrDefault<string>("to");
                            var deleteAction = reader.GetValueOrDefault<string>("on_delete");
                            var fkOrdinal = reader.GetValueOrDefault<int>("seq");

                            Logger.LogTrace(SqliteDesignStrings.FoundForeignKeyColumn(
                                dependentTable.Name, id, principalTableName, fromColumnName,
                                toColumnName, deleteAction, fkOrdinal));

                            ForeignKeyModel foreignKey;
                            if (!tableForeignKeys.TryGetValue(id, out foreignKey))
                            {
                                TableModel principalTable;
                                if (!_tables.TryGetValue(principalTableName, out principalTable))
                                {
                                    Logger.LogTrace(SqliteDesignStrings.PrincipalTableNotInSelectionSet(
                                        id, dependentTable.Name, principalTableName));
                                }

                                foreignKey = new ForeignKeyModel
                                {
                                    Table = dependentTable,
                                    PrincipalTable = principalTable,
                                    OnDelete = ConvertToReferentialAction(deleteAction)
                                };
                                tableForeignKeys.Add(id, foreignKey);
                            }

                            var fkColumn = new ForeignKeyColumnModel
                            {
                                Ordinal = fkOrdinal
                            };

                            fkColumn.Column = _tableColumns[ColumnKey(dependentTable, fromColumnName)];

                            if (foreignKey.PrincipalTable != null)
                            {
                                ColumnModel toColumn;
                                if (!_tableColumns.TryGetValue(ColumnKey(foreignKey.PrincipalTable, toColumnName), out toColumn))
                                {
                                    toColumn = new ColumnModel { Name = toColumnName };
                                }
                                fkColumn.PrincipalColumn = toColumn;
                            }

                            foreignKey.Columns.Add(fkColumn);
                        }
                    }

                    foreach (var foreignKey in tableForeignKeys)
                    {
                        dependentTable.ForeignKeys.Add(foreignKey.Value);
                    }
                }
            }
        }

        private static ReferentialAction? ConvertToReferentialAction(string onDeleteAction)
        {
            switch (onDeleteAction.ToUpperInvariant())
            {
                case "RESTRICT":
                    return ReferentialAction.Restrict;

                case "CASCADE":
                    return ReferentialAction.Cascade;

                case "SET NULL":
                    return ReferentialAction.SetNull;

                case "SET DEFAULT":
                    return ReferentialAction.SetDefault;

                case "NO ACTION":
                    return ReferentialAction.NoAction;

                default:
                    return null;
            }
        }
    }
}
