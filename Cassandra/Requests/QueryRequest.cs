﻿namespace Cassandra
{
    internal class QueryRequest : IRequest
    {
        public const byte OpCode = 0x07;

        private readonly int _streamId;
        private readonly string _cqlQuery;
        private readonly ConsistencyLevel _consistency;
        private readonly byte _flags = 0x00;

        public QueryRequest(int streamId, string cqlQuery, ConsistencyLevel consistency, bool tracingEnabled)
        {
            this._streamId = streamId;
            this._cqlQuery = cqlQuery;
            this._consistency = consistency;
            if (tracingEnabled)
                this._flags = 0x02;
        }

        public RequestFrame GetFrame()
        {
            var wb = new BEBinaryWriter();
            wb.WriteFrameHeader(0x01, _flags, (byte) _streamId, OpCode);
            wb.WriteLongString(_cqlQuery);
            wb.WriteInt16((short) _consistency);
            return wb.GetFrame();
        }
    }
}
