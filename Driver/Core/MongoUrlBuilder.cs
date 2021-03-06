﻿/* Copyright 2010-2012 10gen Inc.
*
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

using MongoDB.Bson;
using MongoDB.Driver.Internal;

namespace MongoDB.Driver
{
    /// <summary>
    /// Represents URL style connection strings. This is the recommended connection string style, but see also
    /// MongoConnectionStringBuilder if you wish to use .NET style connection strings.
    /// </summary>
    [Serializable]
    public class MongoUrlBuilder
    {
        // private fields
        // default values are set in ResetValues
        private ConnectionMode _connectionMode;
        private TimeSpan _connectTimeout;
        private string _databaseName;
        private MongoCredentials _defaultCredentials;
        private GuidRepresentation _guidRepresentation;
        private bool _ipv6;
        private TimeSpan _maxConnectionIdleTime;
        private TimeSpan _maxConnectionLifeTime;
        private int _maxConnectionPoolSize;
        private int _minConnectionPoolSize;
        private ReadPreference _readPreference;
        private string _replicaSetName;
        private SafeMode _safeMode;
        private IEnumerable<MongoServerAddress> _servers;
        private bool? _slaveOk;
        private TimeSpan _socketTimeout;
        private double _waitQueueMultiple;
        private int _waitQueueSize;
        private TimeSpan _waitQueueTimeout;

        // constructors
        /// <summary>
        /// Creates a new instance of MongoUrlBuilder.
        /// </summary>
        public MongoUrlBuilder()
        {
            ResetValues();
        }

        /// <summary>
        /// Creates a new instance of MongoUrlBuilder.
        /// </summary>
        /// <param name="url">The initial settings.</param>
        public MongoUrlBuilder(string url)
        {
            Parse(url); // Parse calls ResetValues
        }

        // public properties
        /// <summary>
        /// Gets the actual wait queue size (either WaitQueueSize or WaitQueueMultiple x MaxConnectionPoolSize).
        /// </summary>
        public int ComputedWaitQueueSize
        {
            get
            {
                if (_waitQueueMultiple == 0.0)
                {
                    return _waitQueueSize;
                }
                else
                {
                    return (int)(_waitQueueMultiple * _maxConnectionPoolSize);
                }
            }
        }

        /// <summary>
        /// Gets or sets the connection mode.
        /// </summary>
        public ConnectionMode ConnectionMode
        {
            get { return _connectionMode; }
            set { _connectionMode = value; }
        }

        /// <summary>
        /// Gets or sets the connect timeout.
        /// </summary>
        public TimeSpan ConnectTimeout
        {
            get { return _connectTimeout; }
            set { _connectTimeout = value; }
        }

        /// <summary>
        /// Gets or sets the optional database name.
        /// </summary>
        public string DatabaseName
        {
            get { return _databaseName; }
            set { _databaseName = value; }
        }

        /// <summary>
        /// Gets or sets the default credentials.
        /// </summary>
        public MongoCredentials DefaultCredentials
        {
            get { return _defaultCredentials; }
            set { _defaultCredentials = value; }
        }

        /// <summary>
        /// Gets or sets the representation to use for Guids.
        /// </summary>
        public GuidRepresentation GuidRepresentation
        {
            get { return _guidRepresentation; }
            set { _guidRepresentation = value; }
        }

        /// <summary>
        /// Gets or sets whether to use IPv6.
        /// </summary>
        public bool IPv6
        {
            get { return _ipv6; }
            set { _ipv6 = value; }
        }

        /// <summary>
        /// Gets or sets the max connection idle time.
        /// </summary>
        public TimeSpan MaxConnectionIdleTime
        {
            get { return _maxConnectionIdleTime; }
            set { _maxConnectionIdleTime = value; }
        }

        /// <summary>
        /// Gets or sets the max connection life time.
        /// </summary>
        public TimeSpan MaxConnectionLifeTime
        {
            get { return _maxConnectionLifeTime; }
            set { _maxConnectionLifeTime = value; }
        }

        /// <summary>
        /// Gets or sets the max connection pool size.
        /// </summary>
        public int MaxConnectionPoolSize
        {
            get { return _maxConnectionPoolSize; }
            set { _maxConnectionPoolSize = value; }
        }

        /// <summary>
        /// Gets or sets the min connection pool size.
        /// </summary>
        public int MinConnectionPoolSize
        {
            get { return _minConnectionPoolSize; }
            set { _minConnectionPoolSize = value; }
        }

        /// <summary>
        /// Gets or sets the read preference.
        /// </summary>
        public ReadPreference ReadPreference
        {
            get
            {
                if (_readPreference != null)
                {
                    return _readPreference;
                }
                else if (_slaveOk.HasValue)
                {
                    return ReadPreference.FromSlaveOk(_slaveOk.Value);
                }
                else
                {
                    return null;
                }
            }
            set
            {
                if (_slaveOk.HasValue)
                {
                    throw new InvalidOperationException("ReadPreference cannot be set because SlaveOk already has a value.");
                }
                _readPreference = value;
            }
        }

        /// <summary>
        /// Gets or sets the name of the replica set.
        /// </summary>
        public string ReplicaSetName
        {
            get { return _replicaSetName; }
            set { _replicaSetName = value; }
        }

        /// <summary>
        /// Gets or sets the SafeMode to use.
        /// </summary>
        public SafeMode SafeMode
        {
            get { return _safeMode; }
            set { _safeMode = value; }
        }

        /// <summary>
        /// Gets or sets the address of the server (see also Servers if using more than one address).
        /// </summary>
        public MongoServerAddress Server
        {
            get { return (_servers == null) ? null : _servers.Single(); }
            set { _servers = new MongoServerAddress[] { value }; }
        }

        /// <summary>
        /// Gets or sets the list of server addresses (see also Server if using only one address).
        /// </summary>
        public IEnumerable<MongoServerAddress> Servers
        {
            get { return _servers; }
            set { _servers = value; }
        }

        /// <summary>
        /// Gets or sets whether queries should be sent to secondary servers.
        /// </summary>
        [Obsolete("Use ReadPreference instead.")]
        public bool SlaveOk
        {
            get
            {
                if (_slaveOk.HasValue)
                {
                    return _slaveOk.Value;
                }
                else if (_readPreference != null)
                {
                    return _readPreference.ToSlaveOk();
                }
                else
                {
                    return false;
                }
            }
            set
            {
                if (_readPreference != null)
                {
                    throw new InvalidOperationException("SlaveOk cannot be set because ReadPreference already has a value.");
                }
                _slaveOk = value;
            }
        }

        /// <summary>
        /// Gets or sets the socket timeout.
        /// </summary>
        public TimeSpan SocketTimeout
        {
            get { return _socketTimeout; }
            set { _socketTimeout = value; }
        }

        /// <summary>
        /// Gets or sets the wait queue multiple (the actual wait queue size will be WaitQueueMultiple x MaxConnectionPoolSize).
        /// </summary>
        public double WaitQueueMultiple
        {
            get { return _waitQueueMultiple; }
            set
            {
                _waitQueueMultiple = value;
                _waitQueueSize = 0;
            }
        }

        /// <summary>
        /// Gets or sets the wait queue size.
        /// </summary>
        public int WaitQueueSize
        {
            get { return _waitQueueSize; }
            set
            {
                _waitQueueMultiple = 0;
                _waitQueueSize = value;
            }
        }

        /// <summary>
        /// Gets or sets the wait queue timeout.
        /// </summary>
        public TimeSpan WaitQueueTimeout
        {
            get { return _waitQueueTimeout; }
            set { _waitQueueTimeout = value; }
        }

        // internal static methods
        // these helper methods are shared with MongoConnectionStringBuilder
        internal static string FormatTimeSpan(TimeSpan value)
        {
            const int msInOneSecond = 1000; // milliseconds
            const int msInOneMinute = 60 * msInOneSecond;
            const int msInOneHour = 60 * msInOneMinute;

            var ms = (int)value.TotalMilliseconds;
            if ((ms % msInOneHour) == 0)
            {
                return string.Format("{0}h", ms / msInOneHour);
            }
            else if ((ms % msInOneMinute) == 0 && ms < msInOneHour)
            {
                return string.Format("{0}m", ms / msInOneMinute);
            }
            else if ((ms % msInOneSecond) == 0 && ms < msInOneMinute)
            {
                return string.Format("{0}s", ms / msInOneSecond);
            }
            else if (ms < 1000)
            {
                return string.Format("{0}ms", ms);
            }
            else
            {
                return value.ToString();
            }
        }

        internal static ConnectionMode ParseConnectionMode(string name, string s)
        {
            try
            {
                return (ConnectionMode)Enum.Parse(typeof(ConnectionMode), s, true); // ignoreCase
            }
            catch (ArgumentException)
            {
                throw new FormatException(FormatMessage(name, s));
            }
        }

        internal static bool ParseBoolean(string name, string s)
        {
            try
            {
                return XmlConvert.ToBoolean(s.ToLower());
            }
            catch (FormatException)
            {
                throw new FormatException(FormatMessage(name, s));
            }
        }

        internal static double ParseDouble(string name, string s)
        {
            try
            {
                return XmlConvert.ToDouble(s);
            }
            catch (FormatException)
            {
                throw new FormatException(FormatMessage(name, s));
            }
        }

        internal static int ParseInt32(string name, string s)
        {
            try
            {
                return XmlConvert.ToInt32(s);
            }
            catch (FormatException)
            {
                throw new FormatException(FormatMessage(name, s));
            }
        }

        internal static ReadPreferenceMode ParseReadPreferenceMode(string name, string s)
        {
            try
            {
                return (ReadPreferenceMode)Enum.Parse(typeof(ReadPreferenceMode), s, true); // ignoreCase
            }
            catch (ArgumentException)
            {
                throw new FormatException(FormatMessage(name, s));
            }
        }

        internal static ReplicaSetTagSet ParseReplicaSetTagSet(string name, string s)
        {
            var tagSet = new ReplicaSetTagSet();
            foreach (var tagString in s.Split(','))
            {
                var parts = tagString.Split(':');
                if (parts.Length != 2)
                {
                    throw new FormatException(FormatMessage(name, s));
                }
                var tag = new ReplicaSetTag(parts[0].Trim(), parts[1].Trim());
                tagSet.Add(tag);
            }
            return tagSet;
        }

        internal static TimeSpan ParseTimeSpan(string name, string s)
        {
            TimeSpan result;
            if (TryParseTimeSpan(name, s, out result))
            {
                return result;
            }
            else
            {
                throw new FormatException(FormatMessage(name, s));
            }
        }

        internal static bool TryParseTimeSpan(string name, string s, out TimeSpan result)
        {
            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(s))
            {
                name = name.ToLower();
                s = s.ToLower();
                var end = s.Length - 1;

                var multiplier = 1000; // default units are seconds
                if (name.EndsWith("ms", StringComparison.Ordinal))
                {
                    multiplier = 1;
                }
                else if (s.EndsWith("ms", StringComparison.Ordinal))
                {
                    s = s.Substring(0, s.Length - 2);
                    multiplier = 1;
                }
                else if (s[end] == 's')
                {
                    s = s.Substring(0, s.Length - 1);
                    multiplier = 1000;
                }
                else if (s[end] == 'm')
                {
                    s = s.Substring(0, s.Length - 1);
                    multiplier = 60 * 1000;
                }
                else if (s[end] == 'h')
                {
                    s = s.Substring(0, s.Length - 1);
                    multiplier = 60 * 60 * 1000;
                }
                else if (s.IndexOf(':') != -1)
                {
                    return TimeSpan.TryParse(s, out result);
                }

                try
                {
                    result = TimeSpan.FromMilliseconds(multiplier * XmlConvert.ToDouble(s));
                    return true;
                }
                catch (FormatException)
                {
                    result = default(TimeSpan);
                    return false;
                }
            }

            result = default(TimeSpan);
            return false;
        }

        // private static methods
        private static string FormatMessage(string name, string value)
        {
            return string.Format("Invalid key value pair in connection string. {0}='{1}'.", name, value);
        }

        // public methods
        /// <summary>
        /// Parses a URL and sets all settings to match the URL.
        /// </summary>
        /// <param name="url">The URL.</param>
        public void Parse(string url)
        {
            ResetValues();
            const string serverPattern = @"((\[[^]]+?\]|[^:,/]+)(:\d+)?)";
            const string pattern =
                @"^mongodb://" +
                @"((?<username>[^:]+):(?<password>[^@]+)@)?" +
                @"(?<servers>" + serverPattern + "(," + serverPattern + ")*)" +
                @"(/(?<database>[^?]+)?(\?(?<query>.*))?)?$";
            Match match = Regex.Match(url, pattern);
            if (match.Success)
            {
                string username = Uri.UnescapeDataString(match.Groups["username"].Value);
                string password = Uri.UnescapeDataString(match.Groups["password"].Value);
                string servers = match.Groups["servers"].Value;
                string databaseName = match.Groups["database"].Value;
                string query = match.Groups["query"].Value;

                if (username != "" && password != "")
                {
                    _defaultCredentials = new MongoCredentials(username, password);
                }
                else
                {
                    _defaultCredentials = null;
                }

                if (servers != "")
                {
                    List<MongoServerAddress> addresses = new List<MongoServerAddress>();
                    foreach (string server in servers.Split(','))
                    {
                        var address = MongoServerAddress.Parse(server);
                        addresses.Add(address);
                    }
                    _servers = addresses;
                }
                else
                {
                    throw new FormatException("Invalid connection string. Server missing.");
                }

                _databaseName = (databaseName != "") ? databaseName : null;

                if (!string.IsNullOrEmpty(query))
                {
                    foreach (var pair in query.Split('&', ';'))
                    {
                        var parts = pair.Split('=');
                        if (parts.Length != 2)
                        {
                            throw new FormatException(string.Format("Invalid connection string '{0}'.", parts));
                        }
                        var name = parts[0];
                        var value = parts[1];

                        switch (name.ToLower())
                        {
                            case "connect":
                                _connectionMode = ParseConnectionMode(name, value);
                                break;
                            case "connecttimeout":
                            case "connecttimeoutms":
                                _connectTimeout = ParseTimeSpan(name, value);
                                break;
                            case "fsync":
                                if (_safeMode == null) { _safeMode = new SafeMode(false); }
                                _safeMode.FSync = ParseBoolean(name, value);
                                break;
                            case "guids":
                            case "uuidrepresentation":
                                _guidRepresentation = (GuidRepresentation)Enum.Parse(typeof(GuidRepresentation), value, true); // ignoreCase
                                break;
                            case "ipv6":
                                _ipv6 = ParseBoolean(name, value);
                                break;
                            case "j":
                            case "journal":
                                if (_safeMode == null) { _safeMode = new SafeMode(false); }
                                SafeMode.Journal = ParseBoolean(name, value);
                                break;
                            case "maxidletime":
                            case "maxidletimems":
                                _maxConnectionIdleTime = ParseTimeSpan(name, value);
                                break;
                            case "maxlifetime":
                            case "maxlifetimems":
                                _maxConnectionLifeTime = ParseTimeSpan(name, value);
                                break;
                            case "maxpoolsize":
                                _maxConnectionPoolSize = ParseInt32(name, value);
                                break;
                            case "minpoolsize":
                                _minConnectionPoolSize = ParseInt32(name, value);
                                break;
                            case "readpreference":
                                if (_readPreference == null) { _readPreference = new ReadPreference(); }
                                _readPreference.ReadPreferenceMode = ParseReadPreferenceMode(name, value);
                                break;
                            case "readpreferencetags":
                                if (_readPreference == null) { _readPreference = new ReadPreference { ReadPreferenceMode = ReadPreferenceMode.Primary }; }
                                _readPreference.AddTagSet(ParseReplicaSetTagSet(name, value));
                                break;
                            case "replicaset":
                                _replicaSetName = value;
                                break;
                            case "safe":
                                if (_safeMode == null) { _safeMode = new SafeMode(false); }
                                SafeMode.Enabled = ParseBoolean(name, value);
                                break;
                            case "slaveok":
                                _slaveOk = ParseBoolean(name, value);
                                break;
                            case "sockettimeout":
                            case "sockettimeoutms":
                                _socketTimeout = ParseTimeSpan(name, value);
                                break;
                            case "w":
                                if (_safeMode == null) { _safeMode = new SafeMode(false); }
                                try
                                {
                                    SafeMode.W = ParseInt32(name, value);
                                }
                                catch (FormatException)
                                {
                                    SafeMode.WMode = value;
                                }
                                break;
                            case "waitqueuemultiple":
                                _waitQueueMultiple = ParseDouble(name, value);
                                _waitQueueSize = 0;
                                break;
                            case "waitqueuesize":
                                _waitQueueMultiple = 0;
                                _waitQueueSize = ParseInt32(name, value);
                                break;
                            case "waitqueuetimeout":
                            case "waitqueuetimeoutms":
                                _waitQueueTimeout = ParseTimeSpan(name, value);
                                break;
                            case "wtimeout":
                            case "wtimeoutms":
                                if (_safeMode == null) { _safeMode = new SafeMode(false); }
                                SafeMode.WTimeout = ParseTimeSpan(name, value);
                                break;
                        }
                    }
                }
            }
            else
            {
                throw new FormatException(string.Format("Invalid connection string '{0}'.", url));
            }
        }

        /// <summary>
        /// Creates a new instance of MongoUrl based on the settings in this MongoUrlBuilder.
        /// </summary>
        /// <returns>A new instance of MongoUrl.</returns>
        public MongoUrl ToMongoUrl()
        {
            return MongoUrl.Create(ToString());
        }

        /// <summary>
        /// Creates a new instance of MongoServerSettings based on the settings in this MongoUrlBuilder.
        /// </summary>
        /// <returns>A new instance of MongoServerSettings.</returns>
        public MongoServerSettings ToServerSettings()
        {
            var readPreference = ReadPreference ?? ReadPreference.Primary;
            return new MongoServerSettings(_connectionMode, _connectTimeout, null, _defaultCredentials, _guidRepresentation, _ipv6,
                _maxConnectionIdleTime, _maxConnectionLifeTime, _maxConnectionPoolSize, _minConnectionPoolSize, readPreference, _replicaSetName,
                _safeMode ?? MongoDefaults.SafeMode, _servers, _socketTimeout, ComputedWaitQueueSize, _waitQueueTimeout);
        }

        /// <summary>
        /// Returns the canonical URL based on the settings in this MongoUrlBuilder.
        /// </summary>
        /// <returns>The canonical URL.</returns>
        public override string ToString()
        {
            StringBuilder url = new StringBuilder();
            url.Append("mongodb://");
            if (_defaultCredentials != null)
            {
                url.AppendFormat("{0}:{1}@", Uri.EscapeDataString(_defaultCredentials.Username), Uri.EscapeDataString(_defaultCredentials.Password));
            }
            if (_servers != null)
            {
                bool firstServer = true;
                foreach (MongoServerAddress server in _servers)
                {
                    if (!firstServer) { url.Append(","); }
                    if (server.Port == 27017)
                    {
                        url.Append(server.Host);
                    }
                    else
                    {
                        url.AppendFormat("{0}:{1}", server.Host, server.Port);
                    }
                    firstServer = false;
                }
            }
            if (_databaseName != null)
            {
                url.Append("/");
                url.Append(_databaseName);
            }
            var query = new StringBuilder();
            if (_ipv6)
            {
                query.AppendFormat("ipv6=true;");
            }
            if (_connectionMode == ConnectionMode.Direct && _servers != null && _servers.Count() != 1 ||
                _connectionMode == ConnectionMode.ReplicaSet && (_servers == null || _servers.Count() == 1))
            {
                query.AppendFormat("connect={0};", MongoUtils.ToCamelCase(_connectionMode.ToString()));
            }
            if (!string.IsNullOrEmpty(_replicaSetName))
            {
                query.AppendFormat("replicaSet={0};", _replicaSetName);
            }
            if (_slaveOk.HasValue)
            {
                query.AppendFormat("slaveOk={0};", _slaveOk.Value ? "true" : "false"); // note: bool.ToString() returns "True" and "False"
            }
            if (_readPreference != null)
            {
                query.AppendFormat("readPreference={0};", MongoUtils.ToCamelCase(_readPreference.ReadPreferenceMode.ToString()));
                if (_readPreference.TagSets != null)
                {
                    foreach (var tagSet in _readPreference.TagSets)
                    {
                        query.AppendFormat("readPreferenceTags={0};", string.Join(",", tagSet.Select(t => string.Format("{0}:{1}", t.Name, t.Value)).ToArray()));
                    }
                }
            }
            if (_safeMode != null && _safeMode.Enabled)
            {
                query.AppendFormat("safe=true;");
                if (_safeMode.FSync)
                {
                    query.Append("fsync=true;");
                }
                if (_safeMode.Journal)
                {
                    query.Append("journal=true;");
                }
                if (_safeMode.W != 0 || _safeMode.WMode != null)
                {
                    if (_safeMode.W != 0)
                    {
                        query.AppendFormat("w={0};", _safeMode.W);
                    }
                    else
                    {
                        query.AppendFormat("w={0};", _safeMode.WMode);
                    }
                    if (_safeMode.WTimeout != TimeSpan.Zero)
                    {
                        query.AppendFormat("wtimeout={0};", FormatTimeSpan(_safeMode.WTimeout));
                    }
                }
            }
            if (_connectTimeout != MongoDefaults.ConnectTimeout)
            {
                query.AppendFormat("connectTimeout={0};", FormatTimeSpan(_connectTimeout));
            }
            if (_maxConnectionIdleTime != MongoDefaults.MaxConnectionIdleTime)
            {
                query.AppendFormat("maxIdleTime={0};", FormatTimeSpan(_maxConnectionIdleTime));
            }
            if (_maxConnectionLifeTime != MongoDefaults.MaxConnectionLifeTime)
            {
                query.AppendFormat("maxLifeTime={0};", FormatTimeSpan(_maxConnectionLifeTime));
            }
            if (_maxConnectionPoolSize != MongoDefaults.MaxConnectionPoolSize)
            {
                query.AppendFormat("maxPoolSize={0};", _maxConnectionPoolSize);
            }
            if (_minConnectionPoolSize != MongoDefaults.MinConnectionPoolSize)
            {
                query.AppendFormat("minPoolSize={0};", _minConnectionPoolSize);
            }
            if (_socketTimeout != MongoDefaults.SocketTimeout)
            {
                query.AppendFormat("socketTimeout={0};", FormatTimeSpan(_socketTimeout));
            }
            if (_waitQueueMultiple != 0.0 && _waitQueueMultiple != MongoDefaults.WaitQueueMultiple)
            {
                query.AppendFormat("waitQueueMultiple={0};", _waitQueueMultiple);
            }
            if (_waitQueueSize != 0 && _waitQueueSize != MongoDefaults.WaitQueueSize)
            {
                query.AppendFormat("waitQueueSize={0};", _waitQueueSize);
            }
            if (_waitQueueTimeout != MongoDefaults.WaitQueueTimeout)
            {
                query.AppendFormat("waitQueueTimeout={0};", FormatTimeSpan(WaitQueueTimeout));
            }
            if (_guidRepresentation != MongoDefaults.GuidRepresentation)
            {
                query.AppendFormat("uuidRepresentation={0};", _guidRepresentation);
            }
            if (query.Length != 0)
            {
                query.Length = query.Length - 1; // remove trailing ";"
                if (_databaseName == null)
                {
                    url.Append("/");
                }
                url.Append("?");
                url.Append(query.ToString());
            }
            return url.ToString();
        }

        // private methods
        private void ResetValues()
        {
            _connectionMode = ConnectionMode.Automatic;
            _connectTimeout = MongoDefaults.ConnectTimeout;
            _databaseName = null;
            _defaultCredentials = null;
            _guidRepresentation = MongoDefaults.GuidRepresentation;
            _ipv6 = false;
            _maxConnectionIdleTime = MongoDefaults.MaxConnectionIdleTime;
            _maxConnectionLifeTime = MongoDefaults.MaxConnectionLifeTime;
            _maxConnectionPoolSize = MongoDefaults.MaxConnectionPoolSize;
            _minConnectionPoolSize = MongoDefaults.MinConnectionPoolSize;
            _readPreference = null;
            _replicaSetName = null;
            _safeMode = null;
            _servers = null;
            _slaveOk = null;
            _socketTimeout = MongoDefaults.SocketTimeout;
            _waitQueueMultiple = MongoDefaults.WaitQueueMultiple;
            _waitQueueSize = MongoDefaults.WaitQueueSize;
            _waitQueueTimeout = MongoDefaults.WaitQueueTimeout;
        }
    }
}
