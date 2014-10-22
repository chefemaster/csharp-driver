//
//      Copyright (C) 2012-2014 DataStax Inc.
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.
//

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;

namespace Cassandra
{
    /// <summary>
    ///  Keeps metadata on the connected cluster, including known nodes and schema
    ///  definitions.
    /// </summary>
    public class Metadata : IDisposable
    {
        private readonly Hosts _hosts;
        private volatile TokenMap _tokenMap;
        public event HostsEventHandler HostsEvent;
        public event SchemaChangedEventHandler SchemaChangedEvent;

        /// <summary>
        ///  Returns the name of currently connected cluster.
        /// </summary>
        /// <returns>the Cassandra name of currently connected cluster.</returns>
        public String ClusterName { get; internal set; }

        internal ControlConnection ControlConnection { get; private set; }

        internal Metadata(IReconnectionPolicy rp)
        {
            _hosts = new Hosts(rp);
            _hosts.HostDown += OnHostDown;
        }

        public void Dispose()
        {
            ShutDown();
        }

        internal void SetupControlConnection(ControlConnection controlConnection)
        {
            ControlConnection = controlConnection;
            ControlConnection.Init();
        }


        public Host GetHost(IPAddress address)
        {
            Host host;
            if (_hosts.TryGet(address, out host))
                return host;
            return null;
        }

        internal Host AddHost(IPAddress address)
        {
            _hosts.AddIfNotExistsOrBringUpIfDown(address);
            return GetHost(address);
        }

        internal void RemoveHost(IPAddress address)
        {
            _hosts.RemoveIfExists(address);
        }

        internal void FireSchemaChangedEvent(SchemaChangedEventArgs.Kind what, string keyspace, string table, object sender = null)
        {
            if (SchemaChangedEvent != null)
            {
                SchemaChangedEvent(sender ?? this, new SchemaChangedEventArgs {Keyspace = keyspace, What = what, Table = table});
            }
        }

        internal void SetDownHost(IPAddress address, object sender = null)
        {
            _hosts.SetDownIfExists(address);
        }

        private void OnHostDown(Host h, DateTimeOffset nextUpTime)
        {
            if (HostsEvent != null)
            {
                HostsEvent(this, new HostsEventArgs { IPAddress = h.Address, What = HostsEventArgs.Kind.Down });
            }
        }

        internal void BringUpHost(IPAddress address, object sender = null)
        {
            if (_hosts.AddIfNotExistsOrBringUpIfDown(address))
            {
                if (HostsEvent != null)
                {
                    HostsEvent(sender ?? this, new HostsEventArgs {IPAddress = address, What = HostsEventArgs.Kind.Up});
                }
            }
        }

        /// <summary>
        ///  Returns all known hosts of this cluster.
        /// </summary>
        /// 
        /// <returns>collection of all known hosts of this cluster.</returns>
        public ICollection<Host> AllHosts()
        {
            return _hosts.ToCollection();
        }


        public IEnumerable<IPAddress> AllReplicas()
        {
            return _hosts.AllEndPointsToCollection();
        }

        internal void RebuildTokenMap(string partitioner, Dictionary<IPAddress, HashSet<string>> allTokens)
        {
            _tokenMap = TokenMap.Build(partitioner, allTokens);
        }

        public ICollection<IPAddress> GetReplicas(byte[] partitionKey)
        {
            if (_tokenMap == null)
            {
                return new List<IPAddress>();
            }
            return _tokenMap.GetReplicas(_tokenMap.Factory.Hash(partitionKey));
        }


        /// <summary>
        ///  Returns a collection of all defined keyspaces names.
        /// </summary>
        /// 
        /// <returns>a collection of all defined keyspaces names.</returns>
        public ICollection<string> GetKeyspaces()
        {
            return ControlConnection.GetKeyspaces();
        }


        /// <summary>
        ///  Returns metadata of specified keyspace.
        /// </summary>
        /// <param name="keyspace"> the name of the keyspace for which metadata should be
        ///  returned. </param>
        /// 
        /// <returns>the metadata of the requested keyspace or <c>null</c> if
        ///  <c>* keyspace</c> is not a known keyspace.</returns>
        public KeyspaceMetadata GetKeyspace(string keyspace)
        {
            return ControlConnection.GetKeyspace(keyspace);
        }

        /// <summary>
        ///  Returns names of all tables which are defined within specified keyspace.
        /// </summary>
        /// <param name="keyspace">the name of the keyspace for which all tables metadata should be
        ///  returned.</param>
        /// <returns>an ICollection of the metadata for the tables defined in this
        ///  keyspace.</returns>
        public ICollection<string> GetTables(string keyspace)
        {
            return ControlConnection.GetTables(keyspace);
        }


        /// <summary>
        ///  Returns TableMetadata for specified table in specified keyspace.
        /// </summary>
        /// <param name="keyspace">name of the keyspace within specified table is definied.</param>
        /// <param name="tableName">name of table for which metadata should be returned.</param>
        /// <returns>a TableMetadata for the specified table in the specified keyspace.</returns>
        public TableMetadata GetTable(string keyspace, string tableName)
        {
            return ControlConnection.GetTable(keyspace, tableName);
        }

        /// <summary>
        /// Gets the definition associated with a User Defined Type from Cassandra
        /// </summary>
        public UdtColumnInfo GetUdtDefinition(string keyspace, string typeName)
        {
            return ControlConnection.GetUdtDefinition(keyspace, typeName);
        }

        public bool RefreshSchema(string keyspace = null, string table = null)
        {
            ControlConnection.SubmitSchemaRefresh(keyspace, table);
            if (keyspace == null && table == null)
                return ControlConnection.RefreshHosts();
            return true;
        }

        public void ShutDown(int timeoutMs = Timeout.Infinite)
        {
            if (ControlConnection != null)
            {
                ControlConnection.Shutdown(timeoutMs);
            }
        }
    }
}
