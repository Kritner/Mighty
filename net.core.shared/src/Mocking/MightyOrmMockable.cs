using System.Collections.Async;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;

using Mighty.Plugins;
using Mighty.Mapping;
using Mighty.Profiling;
using Mighty.Validation;
using System.Threading;

/// <summary>
/// TO DO: Not sure about putting this in a separate namespace, but maybe best to hide the mockable version?
/// </summary>
namespace Mighty.Mocking
{
	// NEW new:
	//	- Clean support for Single with columns
	//	- Compound PKs
	//	- Cleaner support for sequences (incl. one less DB round-trip on sequence-based insert)
	//	- With the new inner loop this really might be faster than Massive too. 'Kinell.
	//  - True support for ulong for those ADO.NET providers which use it (MySQL...) [CHECK THIS!!]
	//  - Generics(!)
	// To Add:
	//  - Firebird(?)
	// We:
	//  - Solve the problem of default values (https://samsaffron.com/archive/2012/01/16/that-annoying-insert-problem-getting-data-into-the-db-using-dapper)
	//	  by ignoring them at Insert(), but by populating them (in a slightly fake, but working, way) on New()
	//	- Are genuinely cross DB, unlike Dapper Rainbow (and possibly unlike other bits of Dapper?)
	//  - Have a true System.Data hiding interface - you just don't use it *at all* unless you need transactions,
	//	  in which case you use exactly enough of it to manage your transactions, and no more.
	//	- Have an (arguably) nicer/simpler interface to parameter directions and output values than Dapper.

	// Abstract class 'interface' for the ORM and ADO.NET Data Access Wrapper methods.
	// Uses abstract class, not interface, because the semantics of interface mean it can never have anything added to it!
	// (See ... MS document about DB classes; SO post about intefaces)
	//
	// Notes:
	//	- Any params type argument is always last (it has to be)
	//	- DbConnection is always last (or last before a params argument, if any), except in the Single-with-columns overload, where it needs to be where
	//	  it is to play the very useful dual role of also disambiguating calls to this overload from calls to the simpler overload without columns.
	//	- All database parameters (i.e. everything sent to the DB via args, inParams or ioParams) are always passed in as true database
	//	  parameters under all circumstances - they are never interpolated into SQL - so they can never be used for _direct_ SQL injection.
	//	  So assuming you aren't building any SQL to execute yourself within the DB, from the values passed in, then strings etc. which are
	//	  passed in will not need any escaping to be safe.
	//
	// NB MicroOrm is dynamic-focussed, so even when you are using MightyOrm<T> instead of MightyOrm (which is like MightyOrm<dynamic>), the
	// T determines the output type, but not the input type (which can be of type T, but can also be any of the various arbitrary objects
	// which the microORM supports, with appropriately named fields).
	abstract public partial class MightyOrmMockable<T>
	{
		// Abstract class 'interface' for Npgsql cursor control additions.
		// These should ideally be contributed back to Npgsql ([ref]()), but for now are added to MightyOrm.
		// (Note: unfortunately it looks far from trivial to set up a full Npgsql build environment in order to create
		// a properly constructed and tested PR for that project. Which is not to say it won't be done at some point.)
		#region Npgsql cursor dereferencing
		/// <summary>
		/// Should we auto-dereference cursors when using the Npgsql ADO.NET driver? (See Mighty documentation.)
		/// </summary>
		abstract public bool NpgsqlAutoDereferenceCursors { get; set; }

		/// <summary>
		/// How many rows at a time should we fetch if auto-dereferencing cursors on the Npgsql ADO.NET driver? (Default value 10,000.) (See Mighty documentation.)
		/// </summary>
		abstract public int NpgsqlAutoDereferenceFetchSize { get; set; }
		#endregion

		#region Properties
		/// <summary>
		/// Connection string
		/// </summary>
		abstract public string ConnectionString { get; protected set; }

		/// <summary>
		/// ADO.NET provider factory
		/// </summary>
		abstract public DbProviderFactory Factory { get; protected set; }

		/// <summary>
		/// Validator
		/// </summary>
		abstract public Validator Validator { get; protected set; }

		/// <summary>
		/// C# &lt;=&gt; SQL mapper
		/// </summary>
		abstract public SqlNamingMapper SqlMapper { get; protected set; }

		/// <summary>
		/// Optional SQL profiler
		/// </summary>
		abstract public SqlProfiler SqlProfiler { get; protected set; }

		/// <summary>
		/// Table name (null if non-table-specific instance)
		/// </summary>
		abstract public string TableName { get; protected set; }

		/// <summary>
		/// Table owner/schema (null if not specified)
		/// </summary>
		abstract public string TableOwner { get; protected set; }

		/// <summary>
		/// Bare table name (without owner/schema part)
		/// </summary>
		abstract public string BareTableName { get; protected set; }

		/// <summary>
		/// Primary key field or fields (no mapping applied)
		/// </summary>
		abstract public string PrimaryKeyFields { get; protected set; }

		/// <summary>
		/// Separated, lowered primary key fields (no mapping applied)
		/// </summary>
		abstract public List<string> PrimaryKeyList { get; protected set; }

		/// <summary>
		/// All columns in one string, or "*" (mapping, if any, already applied)
		/// </summary>
		abstract public string Columns { get; protected set; }

		/// <summary>
		/// Separated column names, in a list (mapping, if any, already applied)
		/// </summary>
		abstract public List<string> ColumnList { get; protected set; }

		/// <summary>
		/// Sequence name or identity retrieval fn. (always null for compound PK)
		/// </summary>
		abstract public string SequenceNameOrIdentityFn { get; protected set; }

		/// <summary>
		/// Column from which value is retrieved by <see cref="KeyValues"/>
		/// </summary>
		abstract public string ValueColumn { get; protected set; }

		/// <summary>
		/// Table meta data (filtered to be only for columns specified by the generic type T, or by <see cref="columns"/>, where present)
		/// </summary>
		abstract public IEnumerable<dynamic> TableMetaData { get; }
		#endregion

		// Okay, first attempt: always add optional `CancellationToken` after all compulsory params and just before any optional or `params` params

		// 'Interface' for the general purpose data access wrapper methods (i.e. the ones which can be used
		// even if no table has been specified).
		// All versions which simply redirect to other versions are defined here, not in the main class.
		#region Non-table specific methods
		abstract public Task<DbConnection> OpenConnectionAsync();
		abstract public Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken);


		abstract public Task<IAsyncEnumerable<T>> QueryAsync(DbCommand command,
			DbConnection connection = null);
		abstract public Task<IAsyncEnumerable<T>> QueryAsync(DbCommand command,
			CancellationToken cancellationToken,
			DbConnection connection = null);

		abstract public Task<T> SingleAsync(DbCommand command,
			DbConnection connection = null);
		abstract public Task<T> SingleAsync(DbCommand command,
			CancellationToken cancellationToken,
			DbConnection connection = null);

		// no connection, easy args
		abstract public Task<IAsyncEnumerable<T>> QueryAsync(string sql,
			params object[] args);
		abstract public Task<IAsyncEnumerable<T>> QueryAsync(string sql,
			CancellationToken cancellationToken,
			params object[] args);

		abstract public Task<T> SingleFromQueryAsync(string sql,
			params object[] args);
		abstract public Task<T> SingleFromQueryAsync(string sql,
			CancellationToken cancellationToken,
			params object[] args);

		abstract public Task<IAsyncEnumerable<T>> QueryAsync(string sql,
			DbConnection connection,
			params object[] args);
		abstract public Task<IAsyncEnumerable<T>> QueryAsync(string sql,
			DbConnection connection,
			CancellationToken cancellationToken,
			params object[] args);

		abstract public Task<T> SingleFromQueryAsync(string sql,
			DbConnection connection,
			params object[] args);
		abstract public Task<T> SingleFromQueryAsync(string sql,
			DbConnection connection,
			CancellationToken cancellationToken,
			params object[] args);

		abstract public Task<IAsyncEnumerable<T>> QueryWithParamsAsync(string sql,
			object inParams = null, object outParams = null, object ioParams = null, object returnParams = null,
			DbConnection connection = null,
			params object[] args);
		abstract public Task<IAsyncEnumerable<T>> QueryWithParamsAsync(string sql,
			CancellationToken cancellationToken,
			object inParams = null, object outParams = null, object ioParams = null, object returnParams = null,
			DbConnection connection = null,
			params object[] args);

		abstract public Task<T> SingleFromQueryWithParamsAsync(string sql,
			object inParams = null, object outParams = null, object ioParams = null, object returnParams = null,
			DbConnection connection = null,
			params object[] args);
		abstract public Task<T> SingleFromQueryWithParamsAsync(string sql,
			CancellationToken cancellationToken,
			object inParams = null, object outParams = null, object ioParams = null, object returnParams = null,
			DbConnection connection = null,
			params object[] args);

		abstract public Task<IAsyncEnumerable<T>> QueryFromProcedureAsync(string spName,
			object inParams = null, object outParams = null, object ioParams = null, object returnParams = null,
			DbConnection connection = null,
			params object[] args);
		abstract public Task<IAsyncEnumerable<T>> QueryFromProcedureAsync(string spName,
			CancellationToken cancellationToken,
			object inParams = null, object outParams = null, object ioParams = null, object returnParams = null,
			DbConnection connection = null,
			params object[] args);

		abstract public Task<T> SingleFromProcedureAsync(string spName,
			object inParams = null, object outParams = null, object ioParams = null, object returnParams = null,
			DbConnection connection = null,
			params object[] args);
		abstract public Task<T> SingleFromProcedureAsync(string spName,
			CancellationToken cancellationToken,
			object inParams = null, object outParams = null, object ioParams = null, object returnParams = null,
			DbConnection connection = null,
			params object[] args);

		abstract public Task<IAsyncEnumerable<IAsyncEnumerable<T>>> QueryMultipleAsync(DbCommand command,
			DbConnection connection = null);
		abstract public Task<IAsyncEnumerable<IAsyncEnumerable<T>>> QueryMultipleAsync(DbCommand command,
			CancellationToken cancellationToken,
			DbConnection connection = null);

		// no connection, easy args
		abstract public Task<IAsyncEnumerable<IAsyncEnumerable<T>>> QueryMultipleAsync(string sql,
			params object[] args);
		abstract public Task<IAsyncEnumerable<IAsyncEnumerable<T>>> QueryMultipleAsync(string sql,
			CancellationToken cancellationToken,
			params object[] args);

		abstract public Task<IAsyncEnumerable<IAsyncEnumerable<T>>> QueryMultipleAsync(string sql,
			DbConnection connection,
			params object[] args);
		abstract public Task<IAsyncEnumerable<IAsyncEnumerable<T>>> QueryMultipleAsync(string sql,
			DbConnection connection,
			CancellationToken cancellationToken,
			params object[] args);

		abstract public Task<IAsyncEnumerable<IAsyncEnumerable<T>>> QueryMultipleWithParamsAsync(string sql,
			object inParams = null, object outParams = null, object ioParams = null, object returnParams = null,
			DbConnection connection = null,
			params object[] args);
		abstract public Task<IAsyncEnumerable<IAsyncEnumerable<T>>> QueryMultipleWithParamsAsync(string sql,
			CancellationToken cancellationToken,
			object inParams = null, object outParams = null, object ioParams = null, object returnParams = null,
			DbConnection connection = null,
			params object[] args);

		abstract public Task<IAsyncEnumerable<IAsyncEnumerable<T>>> QueryMultipleFromProcedureAsync(string spName,
			object inParams = null, object outParams = null, object ioParams = null, object returnParams = null,
			DbConnection connection = null,
			params object[] args);
		abstract public Task<IAsyncEnumerable<IAsyncEnumerable<T>>> QueryMultipleFromProcedureAsync(string spName,
			CancellationToken cancellationToken,
			object inParams = null, object outParams = null, object ioParams = null, object returnParams = null,
			DbConnection connection = null,
			params object[] args);

		abstract public Task<int> ExecuteAsync(DbCommand command,
			DbConnection connection = null);
		abstract public Task<int> ExecuteAsync(DbCommand command,
			CancellationToken cancellationToken,
			DbConnection connection = null);

		// no connection, easy args
		abstract public Task<int> ExecuteAsync(string sql,
			params object[] args);
		abstract public Task<int> ExecuteAsync(string sql,
			CancellationToken cancellationToken,
			params object[] args);

		abstract public Task<int> ExecuteAsync(string sql,
			DbConnection connection,
			params object[] args);
		abstract public Task<int> ExecuteAsync(string sql,
			DbConnection connection,
			CancellationToken cancellationToken,
			params object[] args);

		/// <summary>
		/// Execute command with parameters
		/// </summary>
		/// <param name="sql"></param>
		/// <param name="inParams"></param>
		/// <param name="outParams"></param>
		/// <param name="ioParams"></param>
		/// <param name="returnParams"></param>
		/// <param name="connection"></param>
		/// <param name="args"></param>
		/// <returns>The results of all non-input parameters</returns>
		abstract public Task<dynamic> ExecuteWithParamsAsync(string sql,
			object inParams = null, object outParams = null, object ioParams = null, object returnParams = null,
			DbConnection connection = null,
			params object[] args);
		abstract public Task<dynamic> ExecuteWithParamsAsync(string sql,
			CancellationToken cancellationToken,
			object inParams = null, object outParams = null, object ioParams = null, object returnParams = null,
			DbConnection connection = null,
			params object[] args);

		/// <summary>
		/// Execute stored procedure with parameters
		/// </summary>
		/// <param name="spName"></param>
		/// <param name="inParams"></param>
		/// <param name="outParams"></param>
		/// <param name="ioParams"></param>
		/// <param name="returnParams"></param>
		/// <param name="connection"></param>
		/// <param name="args"></param>
		/// <returns>The results of all non-input parameters</returns>
		abstract public Task<dynamic> ExecuteProcedureAsync(string spName,
			object inParams = null, object outParams = null, object ioParams = null, object returnParams = null,
			DbConnection connection = null,
			params object[] args);
		abstract public Task<dynamic> ExecuteProcedureAsync(string spName,
			CancellationToken cancellationToken,
			object inParams = null, object outParams = null, object ioParams = null, object returnParams = null,
			DbConnection connection = null,
			params object[] args);

		abstract public Task<object> ScalarAsync(DbCommand command,
			DbConnection connection = null);
		abstract public Task<object> ScalarAsync(DbCommand command,
			CancellationToken cancellationToken,
			DbConnection connection = null);

		// no connection, easy args
		abstract public Task<object> ScalarAsync(string sql,
			params object[] args);
		abstract public Task<object> ScalarAsync(string sql,
			CancellationToken cancellationToken,
			params object[] args);

		abstract public Task<object> ScalarAsync(string sql,
			DbConnection connection,
			params object[] args);
		abstract public Task<object> ScalarAsync(string sql,
			DbConnection connection,
			CancellationToken cancellationToken,
			params object[] args);

		abstract public Task<object> ScalarWithParamsAsync(string sql,
			object inParams = null, object outParams = null, object ioParams = null, object returnParams = null,
			DbConnection connection = null,
			params object[] args);
		abstract public Task<object> ScalarWithParamsAsync(string sql,
			CancellationToken cancellationToken,
			object inParams = null, object outParams = null, object ioParams = null, object returnParams = null,
			DbConnection connection = null,
			params object[] args);

		abstract public Task<object> ScalarFromProcedureAsync(string spName,
			object inParams = null, object outParams = null, object ioParams = null, object returnParams = null,
			DbConnection connection = null,
			params object[] args);
		abstract public Task<object> ScalarFromProcedureAsync(string spName,
			CancellationToken cancellationToken,
			object inParams = null, object outParams = null, object ioParams = null, object returnParams = null,
			DbConnection connection = null,
			params object[] args);

		abstract public Task<PagedResults<T>> PagedFromSelectAsync(string columns, string tablesAndJoins, string where, string orderBy,
			int pageSize = 20, int currentPage = 1,
			DbConnection connection = null,
			params object[] args);
		abstract public Task<PagedResults<T>> PagedFromSelectAsync(string columns, string tablesAndJoins, string where, string orderBy,
			CancellationToken cancellationToken,
			int pageSize = 20, int currentPage = 1,
			DbConnection connection = null,
			params object[] args);

		abstract public DbCommand CreateCommand(string sql,
			params object[] args);

		abstract public DbCommand CreateCommand(string sql,
			DbConnection connection,
			params object[] args);

		abstract public DbCommand CreateCommandWithParams(string sql,
			object inParams = null, object outParams = null, object ioParams = null, object returnParams = null, bool isProcedure = false,
			DbConnection connection = null,
			params object[] args);

		// TO DO: Add kv pair stuff for dropdowns? Maybe, at max, provide a method to convert IAsyncEnumerable<T> to kv pair.
		// ...

		abstract public dynamic ResultsAsExpando(DbCommand cmd);

		abstract protected Task<IAsyncEnumerable<X>> QueryNWithParamsAsync<X>(string sql = null, object inParams = null, object outParams = null, object ioParams = null, object returnParams = null, bool isProcedure = false, CommandBehavior behavior = CommandBehavior.Default, DbConnection connection = null, params object[] args);
		abstract protected Task<IAsyncEnumerable<X>> QueryNWithParamsAsync<X>(CancellationToken cancellationToken, string sql = null, object inParams = null, object outParams = null, object ioParams = null, object returnParams = null, bool isProcedure = false, CommandBehavior behavior = CommandBehavior.Default, DbConnection connection = null, params object[] args);

		abstract protected Task<IAsyncEnumerable<X>> QueryNWithParamsAsync<X>(DbCommand command, CommandBehavior behavior = CommandBehavior.Default, DbConnection connection = null, DbDataReader outerReader = null);
		abstract protected Task<IAsyncEnumerable<X>> QueryNWithParamsAsync<X>(DbCommand command, CancellationToken cancellationToken, CommandBehavior behavior = CommandBehavior.Default, DbConnection connection = null, DbDataReader outerReader = null);
		#endregion

		#region Table specific methods
		/// <summary>
		/// Perform COUNT on current table
		/// </summary>
		/// <param name="columns">Columns (defaults to *, but can be specified, e.g., to count non-nulls in a given field)</param>
		/// <param name="where">Optional where clause</param>
		/// <param name="connection">Optional connection</param>
		/// <param name="args">Args</param>
		/// <returns></returns>
		abstract public Task<object> CountAsync(string columns = "*", string where = null,
			DbConnection connection = null,
			params object[] args);
		abstract public Task<object> CountAsync(CancellationToken cancellationToken, string columns = "*", string where = null,
			DbConnection connection = null,
			params object[] args);

		/// <summary>
		/// Perform scalar operation on the current table (use for SUM, MAX, MIN, AVG, etc.)
		/// </summary>
		/// <param name="expression">Scalar expression</param>
		/// <param name="where">Optional where clause</param>
		/// <param name="connection">Optional connection</param>
		/// <param name="args">Parameters</param>
		/// <returns></returns>
		abstract public Task<object> AggregateAsync(string expression, string where = null,
			DbConnection connection = null,
			params object[] args);
		abstract public Task<object> AggregateAsync(string expression, CancellationToken cancellationToken, string where = null,
			DbConnection connection = null,
			params object[] args);

		/// <summary>
		/// Perform scalar operation on the current table (use for SUM, MAX, MIN, AVG, etc.), with support for named params.
		/// </summary>
		/// <param name="expression">Scalar expression</param>
		/// <param name="where">Optional where clause</param>
		/// <param name="inParams">Optional input parameters</param>
		/// <param name="outParams">Optional output parameters</param>
		/// <param name="ioParams">Optional input-output parameters</param>
		/// <param name="returnParams">Optional return parameters</param>
		/// <param name="connection">Optional connection</param>
		/// <param name="args">Optional auto-named input parameters</param>
		/// <returns></returns>
		abstract public Task<object> AggregateWithParamsAsync(string expression, string where = null,
			object inParams = null, object outParams = null, object ioParams = null, object returnParams = null,
			DbConnection connection = null,
			params object[] args);
		abstract public Task<object> AggregateWithParamsAsync(string expression, CancellationToken cancellationToken, string where = null,
			object inParams = null, object outParams = null, object ioParams = null, object returnParams = null,
			DbConnection connection = null,
			params object[] args);

		/// <summary>
		/// Get a single object from the current table by primary key value
		/// </summary>
		/// <param name="key">Single key (or any reasonable multi-value item for compound keys)</param>
		/// <param name="columns">Optional columns to retrieve</param>
		/// <param name="connection">Optional connection</param>
		/// <returns></returns>
		abstract public Task<T> SingleAsync(object key, string columns = null,
			DbConnection connection = null);
		abstract public Task<T> SingleAsync(object key, CancellationToken cancellationToken, string columns = null,
			DbConnection connection = null);

		/// <summary>
		/// Get a single object from the current table with where specification.
		/// </summary>
		/// <param name="where">Where clause</param>
		/// <param name="args">Optional auto-named params</param>
		/// <returns></returns>
		/// <remarks>
		/// 'Easy-calling' version, optional args straight after where.
		/// </remarks>
		abstract public Task<T> SingleAsync(string where,
			params object[] args);
		abstract public Task<T> SingleAsync(string where,
			CancellationToken cancellationToken,
			params object[] args);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="where"></param>
		/// <param name="connection"></param>
		/// <param name="orderBy"></param>
		/// <param name="columns"></param>
		/// <param name="args"></param>
		/// <returns></returns>
		/// <remarks>
		/// DbConnection coming early (not just before args) in this one case is really useful, as it avoids ambiguity between
		/// the <see cref="columns" /> and <see cref="orderBy" /> strings and optional string args.
		/// </remarks>
		abstract public Task<T> SingleAsync(string where,
			DbConnection connection = null,
			string orderBy = null,
			string columns = null,
			params object[] args);
		abstract public Task<T> SingleAsync(string where,
			CancellationToken cancellationToken,
			DbConnection connection = null,
			string orderBy = null,
			string columns = null,
			params object[] args);

		// WithParams version just in case; allows transactions for a start
		abstract public Task<T> SingleWithParamsAsync(string where, string orderBy = null, string columns = null,
			object inParams = null, object outParams = null, object ioParams = null, object returnParams = null,
			DbConnection connection = null,
			params object[] args);
		abstract public Task<T> SingleWithParamsAsync(string where, CancellationToken cancellationToken, string orderBy = null, string columns = null,
			object inParams = null, object outParams = null, object ioParams = null, object returnParams = null,
			DbConnection connection = null,
			params object[] args);

		// ORM
		abstract public Task<IAsyncEnumerable<T>> AllAsync(
			string where = null, string orderBy = null, string columns = null, int limit = 0,
			params object[] args);
		abstract public Task<IAsyncEnumerable<T>> AllAsync(
			CancellationToken cancellationToken,
			string where = null, string orderBy = null, string columns = null, int limit = 0,
			params object[] args);

		abstract public Task<IAsyncEnumerable<T>> AllWithParamsAsync(
			string where = null, string orderBy = null, string columns = null, int limit = 0,
			object inParams = null, object outParams = null, object ioParams = null, object returnParams = null,
			DbConnection connection = null,
			params object[] args);
		abstract public Task<IAsyncEnumerable<T>> AllWithParamsAsync(
			CancellationToken cancellationToken,
			string where = null, string orderBy = null, string columns = null, int limit = 0,
			object inParams = null, object outParams = null, object ioParams = null, object returnParams = null,
			DbConnection connection = null,
			params object[] args);

		/// <summary>
		/// Table-specific paging; there is also a data wrapper version of paging <see cref="PagedFromSelect"/>.
		/// </summary>
		/// <param name="orderBy">You may provide orderBy, if you don't it will try to order by PK and will produce an exception if there is no PK defined.</param>
		/// <param name="where"></param>
		/// <param name="columns"></param>
		/// <param name="pageSize"></param>
		/// <param name="currentPage"></param>
		/// <param name="connection"></param>
		/// <param name="args"></param>
		/// <returns>The result of the paged query. Result properties are Items, TotalPages, and TotalRecords.</returns>
		/// <remarks>
		/// <see cref="columns"/> parameter is not placed first because it's an override to something we may have alread provided in the constructor
		/// (so we don't want the user to have to non-fluently re-type it, or else type null, every time).
		/// </remarks>
		abstract public Task<PagedResults<T>> PagedAsync(string where = null, string orderBy = null,
			string columns = null,
			int pageSize = 20, int currentPage = 1,
			DbConnection connection = null,
			params object[] args);
		abstract public Task<PagedResults<T>> PagedAsync(CancellationToken cancellationToken, string where = null, string orderBy = null,
			string columns = null,
			int pageSize = 20, int currentPage = 1,
			DbConnection connection = null,
			params object[] args);

		/// <summary>
		/// Save one or more items using params style arguments
		/// </summary>
		/// <param name="items">The items</param>
		/// <returns></returns>
		abstract public Task<int> SaveAsync(params object[] items);
		abstract public Task<int> SaveAsync(CancellationToken cancellationToken, params object[] items);

		/// <summary>
		/// Save one or more items using pre-specified <see cref="DbConnection"/>
		/// </summary>
		/// <param name="connection">The connection</param>
		/// <param name="items">The items</param>
		/// <returns></returns>
		abstract public Task<int> SaveAsync(DbConnection connection, params object[] items);
		abstract public Task<int> SaveAsync(DbConnection connection, CancellationToken cancellationToken, params object[] items);

		/// <summary>
		/// Save array or other <see cref="IEnumerable"/> of items
		/// </summary>
		/// <param name="items">The items</param>
		/// <returns></returns>
		abstract public Task<int> SaveAsync(IEnumerable<object> items);
		abstract public Task<int> SaveAsync(IEnumerable<object> items, CancellationToken cancellationToken);

		/// <summary>
		/// Save array or other <see cref="IEnumerable"/> of items using pre-specified <see cref="DbConnection"/>
		/// </summary>
		/// <param name="connection">The connection</param>
		/// <param name="items">The items</param>
		/// <returns></returns>
		abstract public Task<int> SaveAsync(DbConnection connection, IEnumerable<object> items);
		abstract public Task<int> SaveAsync(DbConnection connection, IEnumerable<object> items, CancellationToken cancellationToken);

		/// <summary>
		/// Insert single item, returning the item sent in but with PK populated.
		/// If you need all fields populated (i.e. you want to get back DB default values for non-PK fields), please create the item using New() before inserting it.
		/// </summary>
		/// <param name="items">The item to insert, in any reasonable format (for MightyOrm&lt;T&gt; this includes, but is not limited to, in instance of type T)</param>
		/// <returns>The inserted item</returns>
		abstract public Task<T> InsertAsync(object item);
		abstract public Task<T> InsertAsync(object item, CancellationToken cancellationToken);

		/// <summary>
		/// Insert one or more items using params style arguments
		/// </summary>
		/// <param name="items">The items</param>
		/// <returns>The number of rows inserted</returns>
		abstract public Task<int> InsertAsync(params object[] items);
		abstract public Task<int> InsertAsync(CancellationToken cancellationToken, params object[] items);

		/// <summary>
		/// Insert one or more items using pre-specified <see cref="DbConnection"/>
		/// </summary>
		/// <param name="connection">The connection</param>
		/// <param name="items">The items</param>
		/// <returns>The number of rows inserted</returns>
		abstract public Task<int> InsertAsync(DbConnection connection, params object[] items);
		abstract public Task<int> InsertAsync(DbConnection connection, CancellationToken cancellationToken, params object[] items);

		/// <summary>
		/// Insert array or other <see cref="IEnumerable"/> of items
		/// </summary>
		/// <param name="items">The items</param>
		/// <returns>The number of rows inserted</returns>
		abstract public Task<int> InsertAsync(IEnumerable<object> items);
		abstract public Task<int> InsertAsync(IEnumerable<object> items, CancellationToken cancellationToken);

		/// <summary>
		/// Insert array or other <see cref="IEnumerable"/> of items using pre-specified <see cref="DbConnection"/>
		/// </summary>
		/// <param name="connection">The connection</param>
		/// <param name="items">The items</param>
		/// <returns>The number of rows inserted</returns>
		abstract public Task<int> InsertAsync(DbConnection connection, IEnumerable<object> items);
		abstract public Task<int> InsertAsync(DbConnection connection, IEnumerable<object> items, CancellationToken cancellationToken);

		/// <summary>
		/// Update one or more items using params style arguments
		/// </summary>
		/// <param name="items">The items</param>
		/// <returns></returns>
		abstract public Task<int> UpdateAsync(params object[] items);
		abstract public Task<int> UpdateAsync(CancellationToken cancellationToken, params object[] items);

		/// <summary>
		/// Update one or more items using pre-specified <see cref="DbConnection"/>
		/// </summary>
		/// <param name="connection">The connection</param>
		/// <param name="items">The items</param>
		/// <returns></returns>
		abstract public Task<int> UpdateAsync(DbConnection connection, params object[] items);
		abstract public Task<int> UpdateAsync(DbConnection connection, CancellationToken cancellationToken, params object[] items);

		/// <summary>
		/// Update array or other <see cref="IEnumerable"/> of items
		/// </summary>
		/// <param name="items">The items</param>
		/// <returns></returns>
		abstract public Task<int> UpdateAsync(IEnumerable<object> items);
		abstract public Task<int> UpdateAsync(IEnumerable<object> items, CancellationToken cancellationToken);

		/// <summary>
		/// Update array or other <see cref="IEnumerable"/> of items using pre-specified <see cref="DbConnection"/>
		/// </summary>
		/// <param name="connection">The connection</param>
		/// <param name="items">The items</param>
		/// <returns></returns>
		abstract public Task<int> UpdateAsync(DbConnection connection, IEnumerable<object> items);
		abstract public Task<int> UpdateAsync(DbConnection connection, IEnumerable<object> items, CancellationToken cancellationToken);

		/// <summary>
		/// Delete one or more items using params style arguments
		/// </summary>
		/// <param name="items">The items</param>
		/// <returns></returns>
		abstract public Task<int> DeleteAsync(params object[] items);
		abstract public Task<int> DeleteAsync(CancellationToken cancellationToken, params object[] items);

		/// <summary>
		/// Delete one or more items using pre-specified <see cref="DbConnection"/>
		/// </summary>
		/// <param name="connection">The connection</param>
		/// <param name="items">The items</param>
		/// <returns></returns>
		abstract public Task<int> DeleteAsync(DbConnection connection, params object[] items);
		abstract public Task<int> DeleteAsync(DbConnection connection, CancellationToken cancellationToken, params object[] items);

		/// <summary>
		/// Delete array or other <see cref="IEnumerable"/> of items
		/// </summary>
		/// <param name="items">The items</param>
		/// <returns></returns>
		abstract public Task<int> DeleteAsync(IEnumerable<object> items);
		abstract public Task<int> DeleteAsync(IEnumerable<object> items, CancellationToken cancellationToken);

		/// <summary>
		/// Delete array or other <see cref="IEnumerable"/> of items using pre-specified <see cref="DbConnection"/>
		/// </summary>
		/// <param name="connection">The connection</param>
		/// <param name="items">The items</param>
		/// <returns></returns>
		abstract public Task<int> DeleteAsync(DbConnection connection, IEnumerable<object> items);
		abstract public Task<int> DeleteAsync(DbConnection connection, IEnumerable<object> items, CancellationToken cancellationToken);

		abstract public T New();

		abstract public T NewFrom(object nameValues = null, bool addNonPresentAsDefaults = true);

		/// <summary>
		/// Apply all fields which are present in item to the row matching key.
		/// We *don't* filter by available columns - call with <see cref="CreateFrom"/>(<see cref="partialItem"/>) to do that.
		/// </summary>
		/// <param name="partialItem"></param>
		/// <param name="key"></param>
		abstract public Task<int> UpdateUsingAsync(object partialItem, object key);
		abstract public Task<int> UpdateUsingAsync(object partialItem, object key, CancellationToken cancellationToken);

		/// <summary>
		/// Apply all fields which are present in item to the row matching key.
		/// We *don't* filter by available columns - call with <see cref="CreateFrom"/>(<see cref="partialItem"/>) to do that.
		/// </summary>
		/// <param name="partialItem"></param>
		/// <param name="key"></param>
		/// <param name="connection"></param>
		abstract public Task<int> UpdateUsingAsync(object partialItem, object key,
			DbConnection connection);
		abstract public Task<int> UpdateUsingAsync(object partialItem, object key,
			DbConnection connection, CancellationToken cancellationToken);

		/// <summary>
		/// Apply all fields which are present in item to all rows matching where clause
		/// for safety you MUST specify the where clause yourself (use "1=1" to update all rows)/
		/// This removes/ignores any PK fields from the action; keeps auto-named params for args,
		/// and uses named params for the update feilds.
		/// </summary>
		/// <param name="partialItem"></param>
		/// <param name="where"></param>
		/// <param name="args"></param>
		/// <returns></returns>
		abstract public Task<int> UpdateUsingAsync(object partialItem, string where,
			params object[] args);
		abstract public Task<int> UpdateUsingAsync(object partialItem, string where,
			CancellationToken cancellationToken,
			params object[] args);

		/// <summary>
		/// Update from fields in the item sent in. If PK has been specified, any primary key fields in the
		/// item are ignored (this is an update, not an insert!). However the item is not filtered to remove fields
		/// not in the table. If you need that, call <see cref="NewFrom"/>(<see cref="partialItem"/>, false) first.
		/// </summary>
		/// <param name="partialItem"></param>
		/// <param name="where"></param>
		/// <param name="connection"></param>
		/// <param name="args"></param>
		abstract public Task<int> UpdateUsingAsync(object partialItem, string where,
			DbConnection connection,
			params object[] args);
		abstract public Task<int> UpdateUsingAsync(object partialItem, string where,
			DbConnection connection,
			CancellationToken cancellationToken,
			params object[] args);

		/// <summary>
		/// Delete rows from ORM table based on WHERE clause.
		/// </summary>
		/// <param name="where">
		/// Non-optional where clause.
		/// Specify "1=1" if you are sure that you want to delete all rows.</param>
		/// <param name="args">Optional auto-named parameters for the WHERE clause</param>
		/// <returns></returns>
		abstract public Task<int> DeleteAsync(string where,
			params object[] args);
		abstract public Task<int> DeleteAsync(string where,
			CancellationToken cancellationToken,
			params object[] args);

		abstract public Task<int> DeleteAsync(string where,
			DbConnection connection,
			params object[] args);
		abstract public Task<int> DeleteAsync(string where,
			DbConnection connection,
			CancellationToken cancellationToken,
			params object[] args);

		abstract public dynamic GetColumnInfo(string column, bool ExceptionOnAbsent = true);

		abstract public object GetColumnDefault(string columnName);

		abstract public List<object> IsValid(object item, OrmAction action = OrmAction.Save);

		abstract public bool HasPrimaryKey(object item);

		abstract public object GetPrimaryKey(object item, bool alwaysArray = false);

		// TO DO: We should still be supporting this
#if KEY_VALUES
		abstract public async Task<IDictionary<string, string>> KeyValuesAsync(string orderBy = "");
		abstract public async Task<IDictionary<string, string>> KeyValuesAsync(CancellationToken cancellationToken, string orderBy = "");
#endif
		#endregion
	}
}
