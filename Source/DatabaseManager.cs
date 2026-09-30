using Microsoft.Data.Sqlite;
using Verse;
using System;
using System.IO;
using System.Runtime.InteropServices;
using SQLitePCL;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Data;

namespace RimStats {
    [StaticConstructorOnStartup]
    public static class DatabaseManager {
        public static readonly string directory = Path.Combine(GenFilePaths.ConfigFolderPath, "RimStats");
        public static readonly string path = Path.Combine(directory, "Data.db");
        public static readonly string connectionString;
        
        private static readonly Dictionary<Type, string> tableRegistry = new Dictionary<Type, string> {
            {typeof(EventData), "Events"},
            {typeof(StatsData), "Stats"}
        };

        static DatabaseManager() {
            // Ensure the configuration directory exists
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

            // Configure SQLite connection string with shared cache for performance
            SqliteConnectionStringBuilder builder = new SqliteConnectionStringBuilder {
                DataSource = path,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared
            };

            connectionString = builder.ConnectionString;

            try {
                BindBinaries();
                Initialize();
            }
            catch (Exception exception) {
                Log.Error($"{RimStatsMod.Prefix} Database init failed: {exception.Message}");
            }
        }

        public static void Initialize() {
            try {
                BindBinaries();

                InitializeTable<EventData>();
                InitializeTable<StatsData>();

                if (RimStatsMod.settings.logEnabled) 
                    Log.Message($"{RimStatsMod.Prefix} Database successfully initialized");
            }
            catch (Exception exception) {
                Log.Error($"{RimStatsMod.Prefix} Error while initializing the database: {exception.Message}");
            }
        }

        public static void InitializeTable<T>() where T : BaseData {
            using (SqliteConnection connection = new SqliteConnection(connectionString)) {
                connection.Open();
                SqliteCommand command = connection.CreateCommand();

                FieldInfo[] fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance);
                List<string> columns = new List<string>{"id INTEGER PRIMARY KEY AUTOINCREMENT"};

                foreach (FieldInfo field in fields) {
                    columns.Add($"{field.Name} {GetSqlType(field.FieldType)}");   
                }

                command.CommandText = $"CREATE TABLE IF NOT EXISTS {tableRegistry[typeof(T)]} ({string.Join(", ", columns)})";
                command.ExecuteNonQuery();
            }
        }

        public static List<T> ExtractData<T>(int randSeed) where T : BaseData {
            List<T> dataList = new List<T>();
            
            if (!tableRegistry.TryGetValue(typeof(T), out string tableName)) {
                throw new KeyNotFoundException($"Type {typeof(T).Name} is not registered in tableRegistry.");
            }

            using (SqliteConnection connection = new SqliteConnection(connectionString)) {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand()) {
                    command.CommandText = $"SELECT * FROM {tableName} WHERE randSeed = @seed ORDER BY tick ASC";
                    command.Parameters.AddWithValue("@seed", randSeed);

                    using (var reader = command.ExecuteReader()) {
                        while (reader.Read()) {
                            T item = MapRowToType<T>(reader);
                            dataList.Add(item);
                        }
                    }
                }
            }
            return dataList;
        }

        private static T MapRowToType<T>(IDataReader reader) where T : BaseData {
            var constructor = typeof(T).GetConstructors(BindingFlags.Public | BindingFlags.Instance)[0];
            
            var parametersInfo = constructor.GetParameters();
            object[] constructorArgs = new object[parametersInfo.Length];

            for (int i = 0; i < parametersInfo.Length; i++) {
                string paramName = parametersInfo[i].Name;
                
                int ordinal = reader.GetOrdinal(paramName);

                if (ordinal != -1 && !reader.IsDBNull(ordinal)) {
                    object rawValue = reader.GetValue(ordinal);
                    constructorArgs[i] = Convert.ChangeType(rawValue, parametersInfo[i].ParameterType);
                } else {
                    constructorArgs[i] = parametersInfo[i].ParameterType.IsValueType 
                                        ? Activator.CreateInstance(parametersInfo[i].ParameterType) 
                                        : null;
                }
            }
            
            return (T)constructor.Invoke(constructorArgs);
        }

        public static bool InsertData<T>(T data) where T : BaseData {
            bool success = false;
            try {
                // Create directory if it doesn't exist
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

                using (SqliteConnection connection = new SqliteConnection(connectionString)) {
                    connection.Open();
                    SqliteCommand command = connection.CreateCommand();

                    Dictionary<string, object> dataDict = data.ToDictionary();

                    string columns = string.Join(", ", dataDict.Keys);
                    string values = string.Join(", ", dataDict.Keys.Select(k => "$" + k));

                    // Add text to command
                    command.CommandText = $"INSERT INTO {tableRegistry[typeof(T)]} ({columns}) VALUES ({values})";

                    // Anti-SQL injection
                    foreach (var entry in dataDict) {
                        command.Parameters.AddWithValue("$" + entry.Key, entry.Value ?? DBNull.Value);
                    }

                    command.ExecuteNonQuery();
                    success = true;
                }
            }
            catch (Exception exception) {
                Log.Error($"{RimStatsMod.Prefix} Error inserting into {tableRegistry[typeof(T)]}. {exception.Message}");
            }

            return success;
        }

        private static string GetSqlType(Type t) {
            if (t == typeof(int) || t == typeof(long)) return "INTEGER";
            if (t == typeof(float) || t == typeof(double)) return "REAL";
            return "TEXT";
        }

        private static bool isBound = false;

        /// <summary>
        /// Binds the appropriate native SQLite library based on the detected operating system.
        /// </summary>
        private static void BindBinaries() {
            if (isBound) return;

            ModMetaData mod = ModLister.GetModWithIdentifier("progme.rimstats");
            if (mod == null) return;

            string binPath = Path.Combine(mod.RootDir.ToString(), "Bin");

            // 1. Determine the library name based on the platform
            string fileName = "";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) 
                fileName = "e_sqlite3.dll";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) 
                fileName = "libe_sqlite3.so";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) 
                fileName = "libe_sqlite3.dylib";

            string fullPath = Path.Combine(binPath, fileName);

            // 2. Load the native library manually BEFORE any SQLitePCL interaction
            if (File.Exists(fullPath)) {
                NativeLibraryLoader.Load(fullPath);
            } else {
                Log.Error($"{RimStatsMod.Prefix} SQLite binary NOT FOUND at: {fullPath}");
                return; // Stop here, database will not work without the binary
            }

            // 3. Set the provider explicitly to use the library we just loaded
            // This tells SQLitePCL: "Don't search for it, I already loaded it into memory"
            raw.SetProvider(new SQLite3Provider_e_sqlite3());
            isBound = true;
            
            // 4. Batteries_V2.Init() is often not needed if you set the provider manually,
            // but if you must call it, do it AFTER setting the provider.
            // If you still get errors, try COMMENTING OUT the line below.
            try {
                Batteries_V2.Init(); 
            } catch { 
                // Sometimes Init() fails because it tries to re-load what we already loaded
            }

            if (RimStatsMod.settings.logEnabled) 
                Log.Message($"{RimStatsMod.Prefix} SQLite library successfully bound from: {binPath}");
        }
    }

    /// <summary>
    /// Helper class to load platform-specific native libraries dynamically.
    /// </summary>
    internal static class NativeLibraryLoader {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string libname);

        [DllImport("libdl.so.2", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr dlopen(string filename, int flags);

        // RTLD_NOW = 2, RTLD_GLOBAL = 256
        private const int RTLD_NOW = 2;
        private const int RTLD_GLOBAL = 256;

        public static void Load(string path) {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                LoadLibrary(path);
            } else {
                // Use dlopen for Linux and macOS
                dlopen(path, RTLD_NOW | RTLD_GLOBAL);
            }
        }
    }
}