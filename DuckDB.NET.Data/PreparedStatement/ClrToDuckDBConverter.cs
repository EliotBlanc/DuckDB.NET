using System.Linq;

namespace DuckDB.NET.Data.PreparedStatement;

internal static class ClrToDuckDBConverter
{
    private static readonly Dictionary<DbType, Func<object, DuckDBValue>> ValueCreators = new()
    {
        { DbType.Guid, value => NativeMethods.Value.DuckDBCreateUuid(((Guid)value).ToHugeInt(false)) },
        { DbType.Currency, value => DecimalToDuckDBValue((decimal)value) },
        { DbType.Boolean, value => NativeMethods.Value.DuckDBCreateBool((bool)value) },
        { DbType.SByte, value => NativeMethods.Value.DuckDBCreateInt8((sbyte)value) },
        { DbType.Int16, value => NativeMethods.Value.DuckDBCreateInt16((short)value) },
        { DbType.Int32, value => NativeMethods.Value.DuckDBCreateInt32((int)value) },
        { DbType.Int64, value => NativeMethods.Value.DuckDBCreateInt64((long)value) },
        { DbType.Byte, value => NativeMethods.Value.DuckDBCreateUInt8((byte)value) },
        { DbType.UInt16, value => NativeMethods.Value.DuckDBCreateUInt16((ushort)value) },
        { DbType.UInt32, value => NativeMethods.Value.DuckDBCreateUInt32((uint)value) },
        { DbType.UInt64, value => NativeMethods.Value.DuckDBCreateUInt64((ulong)value) },
        { DbType.Single, value => NativeMethods.Value.DuckDBCreateFloat((float)value) },
        { DbType.Double, value => NativeMethods.Value.DuckDBCreateDouble((double)value) },
        { DbType.String, value => NativeMethods.Value.DuckDBCreateVarchar((string?)value) },
        { DbType.VarNumeric, value => NativeMethods.Value.DuckDBCreateHugeInt(new((BigInteger)value)) },
        { DbType.Binary, value =>
            {
                var bytes = (byte[])value;
                return NativeMethods.Value.DuckDBCreateBlob(bytes, bytes.Length);
            }
        },
        { DbType.Date, value =>
            {
                var date = (value is DateOnly dateOnly ? (DuckDBDateOnly)dateOnly : (DuckDBDateOnly)value).ToDuckDBDate();
                return NativeMethods.Value.DuckDBCreateDate(date);
            }
        },
        { DbType.Time, value =>
            {
                var time = NativeMethods.DateTimeHelpers.DuckDBToTime(value is TimeOnly timeOnly ? (DuckDBTimeOnly)timeOnly : (DuckDBTimeOnly)value);
                return NativeMethods.Value.DuckDBCreateTime(time);
            }
        },
        { DbType.DateTime, value =>
            {
                var dateTime = (value is DateTime dt ? (DuckDBTimestamp)dt : (DuckDBTimestamp)value).ToDuckDBTimestampStruct();
                return NativeMethods.Value.DuckDBCreateTimestamp(dateTime);
            }
        },
        { DbType.DateTimeOffset, value => NativeMethods.Value.DuckDBCreateTimestampTz(((DateTimeOffset)value).ToTimestampStruct()) },
    };

    public static DuckDBValue ToDuckDBValue(this object? item, DuckDBLogicalType logicalType, DuckDBType duckDBType, DbType dbType)
    {
        if (item.IsNull())
        {
            return NativeMethods.Value.DuckDBCreateNullValue();
        }

        return (duckDBType, item) switch
        {
            (DuckDBType.Boolean, bool value) => NativeMethods.Value.DuckDBCreateBool(value),

            (DuckDBType.TinyInt, _) => TryConvertTo<sbyte>(out var result) ? NativeMethods.Value.DuckDBCreateInt8(result) : NativeMethods.Value.DuckDBCreateVarchar(item.ToString()),
            (DuckDBType.SmallInt, _) => TryConvertTo<short>(out var result) ? NativeMethods.Value.DuckDBCreateInt16(result) : NativeMethods.Value.DuckDBCreateVarchar(item.ToString()),
            (DuckDBType.Integer, _) => TryConvertTo<int>(out var result) ? NativeMethods.Value.DuckDBCreateInt32(result) : NativeMethods.Value.DuckDBCreateVarchar(item.ToString()),
            (DuckDBType.BigInt, _) => TryConvertTo<long>(out var result) ? NativeMethods.Value.DuckDBCreateInt64(result) : NativeMethods.Value.DuckDBCreateVarchar(item.ToString()),

            (DuckDBType.UnsignedTinyInt, _) => TryConvertTo<byte>(out var result) ? NativeMethods.Value.DuckDBCreateUInt8(result) : NativeMethods.Value.DuckDBCreateVarchar(item.ToString()),
            (DuckDBType.UnsignedSmallInt, _) => TryConvertTo<ushort>(out var result) ? NativeMethods.Value.DuckDBCreateUInt16(result) : NativeMethods.Value.DuckDBCreateVarchar(item.ToString()),
            (DuckDBType.UnsignedInteger, _) => TryConvertTo<uint>(out var result) ? NativeMethods.Value.DuckDBCreateUInt32(result) : NativeMethods.Value.DuckDBCreateVarchar(item.ToString()),
            (DuckDBType.UnsignedBigInt, _) => TryConvertTo<ulong>(out var result) ? NativeMethods.Value.DuckDBCreateUInt64(result) : NativeMethods.Value.DuckDBCreateVarchar(item.ToString()),

            (DuckDBType.Float, float value) => NativeMethods.Value.DuckDBCreateFloat(value),
            (DuckDBType.Double, double value) => NativeMethods.Value.DuckDBCreateDouble(value),

            (DuckDBType.Decimal, decimal value) => DecimalToDuckDBValue(value),
            (DuckDBType.HugeInt, BigInteger value) => NativeMethods.Value.DuckDBCreateHugeInt(new DuckDBHugeInt(value)),

            (DuckDBType.Varchar, string value) => NativeMethods.Value.DuckDBCreateVarchar(value),
            (DuckDBType.Uuid, Guid value) => NativeMethods.Value.DuckDBCreateUuid(value.ToHugeInt(false)),

            (DuckDBType.Timestamp, DateTime value) => NativeMethods.Value.DuckDBCreateTimestamp(value.ToTimestampStruct(duckDBType)),
            (DuckDBType.TimestampS, DateTime value) => NativeMethods.Value.DuckDBCreateTimestampS(value.ToTimestampStruct(duckDBType)),
            (DuckDBType.TimestampMs, DateTime value) => NativeMethods.Value.DuckDBCreateTimestampMs(value.ToTimestampStruct(duckDBType)),
            (DuckDBType.TimestampNs, DateTime value) => NativeMethods.Value.DuckDBCreateTimestampNs(value.ToTimestampStruct(duckDBType)),
            (DuckDBType.TimestampTz, DateTime value) => NativeMethods.Value.DuckDBCreateTimestampTz(value.ToTimestampStruct(duckDBType)),
            (DuckDBType.TimestampTz, DateTimeOffset value) => NativeMethods.Value.DuckDBCreateTimestampTz(value.ToTimestampStruct()),
            (DuckDBType.Interval, TimeSpan value) => NativeMethods.Value.DuckDBCreateInterval(value),
            (DuckDBType.Date, DateTime value) => NativeMethods.Value.DuckDBCreateDate(((DuckDBDateOnly)value).ToDuckDBDate()),
            (DuckDBType.Date, DuckDBDateOnly value) => NativeMethods.Value.DuckDBCreateDate(value.ToDuckDBDate()),
            (DuckDBType.Time, DateTime value) => NativeMethods.Value.DuckDBCreateTime(NativeMethods.DateTimeHelpers.DuckDBToTime((DuckDBTimeOnly)value)),
            (DuckDBType.Time, DuckDBTimeOnly value) => NativeMethods.Value.DuckDBCreateTime(NativeMethods.DateTimeHelpers.DuckDBToTime(value)),
            (DuckDBType.Date, DateOnly value) => NativeMethods.Value.DuckDBCreateDate(((DuckDBDateOnly)value).ToDuckDBDate()),
            (DuckDBType.Time, TimeOnly value) => NativeMethods.Value.DuckDBCreateTime(NativeMethods.DateTimeHelpers.DuckDBToTime(value)),
            (DuckDBType.TimeTz, DateTimeOffset value) => NativeMethods.Value.DuckDBCreateTimeTz(value.ToTimeTzStruct()),
            (DuckDBType.Blob, byte[] value) => NativeMethods.Value.DuckDBCreateBlob(value, value.Length),
            (DuckDBType.List, ICollection value) => CreateCollectionValue(logicalType, value, true, dbType),
            (DuckDBType.Array, ICollection value) => CreateCollectionValue(logicalType, value, false, dbType),
            _ when ValueCreators.TryGetValue(dbType, out var converter) => converter(item),
            _ => CreateValueWhenPreparedTypeIsUnknown(duckDBType, item, dbType)
        };

        bool TryConvertTo<T>(out T result) where T : struct
        {
            try
            {
                if (item is T parsable)
                {
                    result = parsable;
                    return true;
                }

                result = (T)Convert.ChangeType(item, typeof(T));
                return true;
            }
            catch (Exception)
            {
                result = default;
                return false;
            }
        }
    }

    private static DuckDBValue CreateValueWhenPreparedTypeIsUnknown(DuckDBType duckDBType, object item, DbType dbType)
    {
        if (duckDBType == DuckDBType.Invalid)
        {
            if (item is ICollection collection)
            {
                return CreateInferredCollectionValue(collection, dbType);
            }
        }

        return NativeMethods.Value.DuckDBCreateVarchar(item.ToString());
    }

    private static DuckDBValue CreateInferredCollectionValue(ICollection collection, DbType dbType)
    {
        var childDuckDBType = InferCollectionChildType(collection, dbType);

        DuckDBLogicalType childType;
        if (childDuckDBType == DuckDBType.Decimal)
        {
            byte precision = 38;
            byte scale = 18;
            childType = NativeMethods.LogicalType.DuckDBCreateDecimalType(precision, scale);
        }
        else
        {
            childType = NativeMethods.LogicalType.DuckDBCreateLogicalType(childDuckDBType);
        }

        try
        {
            using var listLogicalType =
                NativeMethods.LogicalType.DuckDBCreateListType(childType);

            var values = new DuckDBValue[collection.Count];
            var index = 0;
            foreach (var item in collection)
            {
                var duckDBValue = item.ToDuckDBValue(childType, childDuckDBType, dbType);
                values[index++] = duckDBValue;
            }

            return NativeMethods.Value.DuckDBCreateListValue(childType, values, values.Length);
        }
        finally
        {
            childType.Dispose();
        }
    }

    private static DuckDBType InferCollectionChildType(ICollection collection, DbType dbType)
    {
        if (dbType != DbType.Object)
        {
            var typeFromDbType = TryInferDuckDBTypeFromDbType(dbType);
            if (typeFromDbType.HasValue)
            {
                return typeFromDbType.Value;
            }
        }

        var firstNonNullValue = collection
            .Cast<object?>()
            .FirstOrDefault(value => !value.IsNull());

        if (firstNonNullValue is null)
        {
            // Pick VARCHAR for empty/all-null lists unless the caller supplied DbType.
            // Without a non-null value, there is no reliable CLR element type.
            return DuckDBType.Varchar;
        }



        return firstNonNullValue switch
        {
            bool => DuckDBType.Boolean,

            sbyte => DuckDBType.TinyInt,
            short => DuckDBType.SmallInt,
            int => DuckDBType.Integer,
            long => DuckDBType.BigInt,

            byte => DuckDBType.UnsignedTinyInt,
            ushort => DuckDBType.UnsignedSmallInt,
            uint => DuckDBType.UnsignedInteger,
            ulong => DuckDBType.UnsignedBigInt,

            float => DuckDBType.Float,
            double => DuckDBType.Double,
            decimal => DuckDBType.Decimal,

            string => DuckDBType.Varchar,
            Guid => DuckDBType.Uuid,

            DateTime => DuckDBType.Timestamp,
            DateTimeOffset => DuckDBType.TimestampTz,
            TimeSpan => DuckDBType.Interval,

            byte[] => DuckDBType.Blob,

            DuckDBDateOnly => DuckDBType.Date,
            DuckDBTimeOnly => DuckDBType.Time,

#if NET6_0_OR_GREATER
            DateOnly => DuckDBType.Date,
            TimeOnly => DuckDBType.Time,
#endif

            BigInteger => DuckDBType.HugeInt,

            _ => DuckDBType.Varchar
        };
    }

    private static DuckDBType? TryInferDuckDBTypeFromDbType(DbType dbType)
    {
        return dbType switch
        {
            DbType.Boolean => DuckDBType.Boolean,

            DbType.SByte => DuckDBType.TinyInt,
            DbType.Int16 => DuckDBType.SmallInt,
            DbType.Int32 => DuckDBType.Integer,
            DbType.Int64 => DuckDBType.BigInt,

            DbType.Byte => DuckDBType.UnsignedTinyInt,
            DbType.UInt16 => DuckDBType.UnsignedSmallInt,
            DbType.UInt32 => DuckDBType.UnsignedInteger,
            DbType.UInt64 => DuckDBType.UnsignedBigInt,

            DbType.Single => DuckDBType.Float,
            DbType.Double => DuckDBType.Double,
            DbType.Decimal or DbType.Currency => DuckDBType.Decimal,

            DbType.String or DbType.AnsiString or DbType.StringFixedLength or DbType.AnsiStringFixedLength =>
                DuckDBType.Varchar,

            DbType.Guid => DuckDBType.Uuid,

            DbType.Date => DuckDBType.Date,
            DbType.Time => DuckDBType.Time,
            DbType.DateTime or DbType.DateTime2 => DuckDBType.Timestamp,
            DbType.DateTimeOffset => DuckDBType.TimestampTz,

            DbType.Binary => DuckDBType.Blob,

            _ => null
        };
    }

    private static DuckDBValue CreateCollectionValue(DuckDBLogicalType logicalType, ICollection collection, bool isList, DbType dbType)
    {
        using var collectionItemType = isList ? NativeMethods.LogicalType.DuckDBListTypeChildType(logicalType) :
                                                NativeMethods.LogicalType.DuckDBArrayTypeChildType(logicalType);

        var duckDBType = NativeMethods.LogicalType.DuckDBGetTypeId(collectionItemType);

        var values = new DuckDBValue[collection.Count];

        var index = 0;
        foreach (var item in collection)
        {
            var duckDBValue = item.ToDuckDBValue(collectionItemType, duckDBType, dbType);
            values[index] = duckDBValue;
            index++;
        }

        return isList ? NativeMethods.Value.DuckDBCreateListValue(collectionItemType, values, collection.Count)
                      : NativeMethods.Value.DuckDBCreateArrayValue(collectionItemType, values, collection.Count);
    }

    private static DuckDBValue DecimalToDuckDBValue(decimal value)
    {
        var mantissa = value.GetMantissa();

        var width = mantissa.IsZero
            ? value.Scale + 1
            : Math.Max((int)BigInteger.Log10(BigInteger.Abs(mantissa)) + 1, value.Scale + 1);

        return NativeMethods.Value.DuckDBCreateDecimal(new DuckDBDecimal((byte)width, value.Scale, new DuckDBHugeInt(mantissa)));
    }
}
