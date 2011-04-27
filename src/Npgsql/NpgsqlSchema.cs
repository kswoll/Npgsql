// Npgsql.NpgsqlCommand.cs
//
// Author:
//  Josh Cooley <jbnpgsql@tuxinthebox.net>
//
//  Copyright (C) 2002-2005 The Npgsql Development Team
//  npgsql-general@gborg.postgresql.org
//  http://gborg.postgresql.org/project/npgsql/projdisplay.php
//
// Permission to use, copy, modify, and distribute this software and its
// documentation for any purpose, without fee, and without a written
// agreement is hereby granted, provided that the above copyright notice
// and this paragraph and the following two paragraphs appear in all copies.
// 
// IN NO EVENT SHALL THE NPGSQL DEVELOPMENT TEAM BE LIABLE TO ANY PARTY
// FOR DIRECT, INDIRECT, SPECIAL, INCIDENTAL, OR CONSEQUENTIAL DAMAGES,
// INCLUDING LOST PROFITS, ARISING OUT OF THE USE OF THIS SOFTWARE AND ITS
// DOCUMENTATION, EVEN IF THE NPGSQL DEVELOPMENT TEAM HAS BEEN ADVISED OF
// THE POSSIBILITY OF SUCH DAMAGE.
// 
// THE NPGSQL DEVELOPMENT TEAM SPECIFICALLY DISCLAIMS ANY WARRANTIES,
// INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY
// AND FITNESS FOR A PARTICULAR PURPOSE. THE SOFTWARE PROVIDED HEREUNDER IS
// ON AN "AS IS" BASIS, AND THE NPGSQL DEVELOPMENT TEAM HAS NO OBLIGATIONS
// TO PROVIDE MAINTENANCE, SUPPORT, UPDATES, ENHANCEMENTS, OR MODIFICATIONS.


using System;
using System.Data;
using System.IO;
using System.Reflection;
using System.Text;

namespace Npgsql
{
	/// <summary>
	/// Provides the underlying mechanism for reading schema information.
	/// </summary>
	internal sealed class NpgsqlSchema
	{
		private readonly NpgsqlConnection _connection;

		/// <summary>
		/// Creates an NpgsqlSchema that can read schema information from the database.
		/// </summary>
		/// <param name="connection">An open database connection for reading metadata.</param>
		internal NpgsqlSchema(NpgsqlConnection connection)
		{
			_connection = connection;
		}

		/// <summary>
		/// Returns the MetaDataCollections that lists all possible collections.
		/// </summary>
		/// <returns>The MetaDataCollections</returns>
		internal static DataTable GetMetaDataCollections()
        {
            DataSet ds = new DataSet();
            using (Stream xmlStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Npgsql.NpgsqlMetaData.xml"))
            {
                ds.ReadXml(xmlStream);
            }
            return ds.Tables["MetaDataCollections"].Copy();
		}

		/// <summary>
		/// Returns the Restrictions that contains the meaning and position of the values in the restrictions array.
		/// </summary>
		/// <returns>The Restrictions</returns>
		internal static DataTable GetRestrictions()
        {
            DataSet ds = new DataSet();
            using (Stream xmlStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Npgsql.NpgsqlMetaData.xml"))
            {
                ds.ReadXml(xmlStream);
            }
            return ds.Tables["Restrictions"].Copy();
		}

        private NpgsqlCommand BuildCommand(StringBuilder query, string[] restrictions, params string[] names)
        {
            return BuildCommand(query, restrictions, true, names);
        }

		private NpgsqlCommand BuildCommand(StringBuilder query, string[] restrictions, bool addWhere, params string[] names)
		{
			NpgsqlCommand command = new NpgsqlCommand();

			if (restrictions != null && names != null)
			{
				for (int i = 0; i < restrictions.Length && i < names.Length; ++i)
				{
					if (restrictions[i] != null && restrictions[i].Length != 0)
					{
						if (addWhere)
						{
							query.Append(" WHERE ");
							addWhere = false;
						}
						else
						{
							query.Append(" AND ");
						}
						query.AppendFormat("{0} = :{0}", names[i]);

						command.Parameters.Add(new NpgsqlParameter(names[i], restrictions[i]));
					}
				}
			}
			command.CommandText = query.ToString();
			command.Connection = _connection;

			return command;
		}

		/// <summary>
		/// Returns the Databases that contains a list of all accessable databases.
		/// </summary>
		/// <param name="restrictions">The restrictions to filter the collection.</param>
		/// <returns>The Databases</returns>
		internal DataTable GetDatabases(string[] restrictions)
		{
			DataTable databases = new DataTable("Databases");

			databases.Columns.AddRange(
				new DataColumn[] {new DataColumn("database_name"), new DataColumn("owner"), new DataColumn("encoding")});

			StringBuilder getDatabases = new StringBuilder();

			getDatabases.Append(
				"SELECT d.datname AS database_name, u.usename AS owner, pg_catalog.pg_encoding_to_char(d.encoding) AS encoding FROM pg_catalog.pg_database d LEFT JOIN pg_catalog.pg_user u ON d.datdba = u.usesysid");

			using (NpgsqlCommand command = BuildCommand(getDatabases, restrictions, "datname"))
			{
				using (NpgsqlDataAdapter adapter = new NpgsqlDataAdapter(command))
				{
					adapter.Fill(databases);
				}
			}

			return databases;
		}

		/// <summary>
		/// Returns the Tables that contains table and view names and the database and schema they come from.
		/// </summary>
		/// <param name="restrictions">The restrictions to filter the collection.</param>
		/// <returns>The Tables</returns>
		internal DataTable GetTables(string[] restrictions)
		{
			DataTable tables = new DataTable("Tables");

			tables.Columns.AddRange(
				new DataColumn[]
					{
						new DataColumn("table_catalog"), new DataColumn("table_schema"), new DataColumn("table_name"),
						new DataColumn("table_type")
					});

			StringBuilder getTables = new StringBuilder();

			getTables.Append("SELECT table_catalog, table_schema, table_name, table_type FROM information_schema.tables");

			using (
				NpgsqlCommand command =
					BuildCommand(getTables, restrictions, "table_catalog", "table_schema", "table_name", "table_type"))
			{
				using (NpgsqlDataAdapter adapter = new NpgsqlDataAdapter(command))
				{
					adapter.Fill(tables);
				}
			}

			return tables;
		}

		/// <summary>
		/// Returns the Columns that contains information about columns in tables. 
		/// </summary>
		/// <param name="restrictions">The restrictions to filter the collection.</param>
		/// <returns>The Columns.</returns>
		internal DataTable GetColumns(string[] restrictions)
		{
			DataTable columns = new DataTable("Columns");

			columns.Columns.AddRange(
				new DataColumn[]
					{
						new DataColumn("table_catalog"), new DataColumn("table_schema"), new DataColumn("table_name"),
						new DataColumn("column_name"), new DataColumn("ordinal_position", typeof (int)), new DataColumn("column_default"),
						new DataColumn("is_nullable"), new DataColumn("data_type"),
						new DataColumn("character_maximum_length", typeof (int)), new DataColumn("character_octet_length", typeof (int)),
						new DataColumn("numeric_precision", typeof (int)), new DataColumn("numeric_precision_radix", typeof (int)),
						new DataColumn("numeric_scale", typeof (int)), new DataColumn("datetime_precision", typeof (int)),
						new DataColumn("character_set_catalog"), new DataColumn("character_set_schema"),
						new DataColumn("character_set_name"), new DataColumn("collation_catalog")
					});

			StringBuilder getColumns = new StringBuilder();

			getColumns.Append(
				"SELECT table_catalog, table_schema, table_name, column_name, ordinal_position, column_default, is_nullable, udt_name AS data_type, character_maximum_length, character_octet_length, numeric_precision, numeric_precision_radix, numeric_scale, datetime_precision, character_set_catalog, character_set_schema, character_set_name, collation_catalog FROM information_schema.columns");

			using (
				NpgsqlCommand command =
					BuildCommand(getColumns, restrictions, "table_catalog", "table_schema", "table_name", "column_name"))
			{
				using (NpgsqlDataAdapter adapter = new NpgsqlDataAdapter(command))
				{
					adapter.Fill(columns);
				}
			}

			return columns;
		}

		/// <summary>
		/// Returns the Views that contains view names and the database and schema they come from.
		/// </summary>
		/// <param name="restrictions">The restrictions to filter the collection.</param>
		/// <returns>The Views</returns>
		internal DataTable GetViews(string[] restrictions)
		{
			DataTable views = new DataTable("Views");

			views.Columns.AddRange(
				new DataColumn[]
					{
						new DataColumn("table_catalog"), new DataColumn("table_schema"), new DataColumn("table_name"),
						new DataColumn("check_option"), new DataColumn("is_updatable")
					});

			StringBuilder getViews = new StringBuilder();

			getViews.Append(
				"SELECT table_catalog, table_schema, table_name, check_option, is_updatable FROM information_schema.views");

			using (NpgsqlCommand command = BuildCommand(getViews, restrictions, "table_catalog", "table_schema", "table_name"))
			{
				using (NpgsqlDataAdapter adapter = new NpgsqlDataAdapter(command))
				{
					adapter.Fill(views);
				}
			}

			return views;
		}

		/// <summary>
		/// Returns the Users containing user names and the sysid of those users.
		/// </summary>
		/// <param name="restrictions">The restrictions to filter the collection.</param>
		/// <returns>The Users.</returns>
		internal DataTable GetUsers(string[] restrictions)
		{
			DataTable users = new DataTable("Users");

			users.Columns.AddRange(new DataColumn[] {new DataColumn("user_name"), new DataColumn("user_sysid", typeof (int))});

			StringBuilder getUsers = new StringBuilder();

			getUsers.Append("SELECT usename as user_name, usesysid as user_sysid FROM pg_catalog.pg_user");

			using (NpgsqlCommand command = BuildCommand(getUsers, restrictions, "usename"))
			{
				using (NpgsqlDataAdapter adapter = new NpgsqlDataAdapter(command))
				{
					adapter.Fill(users);
				}
			}

			return users;
		}

        internal DataTable GetIndexes(string[] restrictions)
        {
            DataTable indexes = new DataTable("Indexes");

            indexes.Columns.AddRange(
                new DataColumn[]
					{
						new DataColumn("table_catalog"), new DataColumn("table_schema"), new DataColumn("table_name"),
						new DataColumn("index_name")
					});

            StringBuilder getIndexes = new StringBuilder();

            getIndexes.Append(
@"select current_database() as table_catalog,
    n.nspname as table_schema,
    t.relname as table_name,
    i.relname as index_name
from
    pg_catalog.pg_class i join
    pg_catalog.pg_index ix ON ix.indexrelid = i.oid join
    pg_catalog.pg_class t ON ix.indrelid = t.oid join
    pg_attribute a on t.oid = a.attrelid left join
    pg_catalog.pg_user u ON u.usesysid = i.relowner left join
    pg_catalog.pg_namespace n ON n.oid = i.relnamespace
where
    i.relkind = 'i'
    and n.nspname not in ('pg_catalog', 'pg_toast')
    and pg_catalog.pg_table_is_visible(i.oid)
    and a.attnum = ANY(ix.indkey)
    and t.relkind = 'r'");

            using (
                NpgsqlCommand command =
                    BuildCommand(getIndexes, restrictions, false, "table_catalog", "table_schema", "table_name", "index_name"))
            {
                using (NpgsqlDataAdapter adapter = new NpgsqlDataAdapter(command))
                {
                    adapter.Fill(indexes);
                }
            }

            return indexes;
        }

        internal DataTable GetIndexColumns(string[] restrictions)
        {
            DataTable indexColumns = new DataTable("IndexColumns");

            indexColumns.Columns.AddRange(
                new DataColumn[]
					{
						new DataColumn("table_catalog"), new DataColumn("table_schema"), new DataColumn("table_name"),
						new DataColumn("index_name"), new DataColumn("column_name")
					});

            StringBuilder getIndexColumns = new StringBuilder();

            getIndexColumns.Append(
@"select current_database() as table_catalog,
    n.nspname as table_schema,
    t.relname as table_name,
    i.relname as index_name,
    a.attname as column_name
from
    pg_class t join
    pg_index ix on t.oid = ix.indrelid join
    pg_class i on ix.indexrelid = i.oid join
    pg_attribute a on t.oid = a.attrelid left join
    pg_namespace n on i.relnamespace = n.oid
where
    i.relkind = 'i'
    and n.nspname not in ('pg_catalog', 'pg_toast')
    and pg_catalog.pg_table_is_visible(i.oid)
    and a.attnum = ANY(ix.indkey)
    and t.relkind = 'r'");

            using (
                NpgsqlCommand command =
                    BuildCommand(getIndexColumns, restrictions, false, "table_catalog", "table_schema", "table_name", "index_name", "column_name"))
            {
                using (NpgsqlDataAdapter adapter = new NpgsqlDataAdapter(command))
                {
                    adapter.Fill(indexColumns);
                }
            }

            return indexColumns;
        }

		internal static DataTable GetDataSourceInformation()
		{
			DataSet ds = new DataSet();
			using (Stream xmlStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Npgsql.NpgsqlMetaData.xml"))
			{
				ds.ReadXml(xmlStream);
			}
			return ds.Tables["DataSourceInformation"].Copy();
		}

	    public static DataTable GetReservedWords()
	    {
	        DataTable table = new DataTable("ReservedWords");
	        table.Columns.Add("ReservedWord", typeof (string));
            // List of keywords taken from PostgreSQL 9.0 reserved words documentation.
	        string[] keywords = new[]
	        {
                "ALL",
                "ANALYSE",
                "ANALYZE",
                "AND",
                "ANY",
                "ARRAY",
                "AS",
                "ASC",
                "ASYMMETRIC",
                "AUTHORIZATION",
                "BINARY",
                "BOTH",
                "CASE",
                "CAST",
                "CHECK",
                "COLLATE",
                "COLUMN",
                "CONCURRENTLY",
                "CONSTRAINT",
                "CREATE",
                "CROSS",
                "CURRENT_CATALOG",
                "CURRENT_DATE",
                "CURRENT_ROLE",
                "CURRENT_SCHEMA",
                "CURRENT_TIME",
                "CURRENT_TIMESTAMP",
                "CURRENT_USER",
                "DEFAULT",
                "DEFERRABLE",
                "DESC",
                "DISTINCT",
                "DO",
                "ELSE",
                "END",
                "EXCEPT",
                "FALSE",
                "FETCH",
                "FOR",
                "FOREIGN",
                "FREEZE",
                "FROM",
                "FULL",
                "GRANT",
                "GROUP",
                "HAVING",
                "ILIKE",
                "IN",
                "INITIALLY",
                "INNER",
                "INTERSECT",
                "INTO",
                "IS",
                "ISNULL",
                "JOIN",
                "LEADING",
                "LEFT",
                "LIKE",
                "LIMIT",
                "LOCALTIME",
                "LOCALTIMESTAMP",
                "NATURAL",
                "NOT",
                "NOTNULL",
                "NULL",
                "OFFSET",
                "ON",
                "ONLY",
                "OR",
                "ORDER",
                "OUTER",
                "OVER",
                "OVERLAPS",
                "PLACING",
                "PRIMARY",
                "REFERENCES",
                "RETURNING",
                "RIGHT",
                "SELECT",
                "SESSION_USER",
                "SIMILAR",
                "SOME",
                "SYMMETRIC",
                "TABLE",
                "THEN",
                "TO",
                "TRAILING",
                "TRUE",
                "UNION",
                "UNIQUE",
                "USER",
                "USING",
                "VARIADIC",
                "VERBOSE",
                "WHEN",
                "WHERE",
                "WINDOW",
                "WITH"
            };
            foreach (string keyword in keywords)
            {
                table.Rows.Add(keyword);
            }
	        return table;
	    }
	}
}
