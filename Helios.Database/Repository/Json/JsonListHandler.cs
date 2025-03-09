using System.Data;
using Dapper;
using Newtonsoft.Json;

namespace Helios.Database.Repository.Json;

public class JsonListManager : SqlMapper.TypeHandler<List<object>>
{
    public override void SetValue(IDbDataParameter parameter, List<object> value)
    {
        parameter.Value = JsonConvert.SerializeObject(value);
    }

    public override List<object> Parse(object value)
    {
        if (value == DBNull.Value)
            return null;

        return JsonConvert.DeserializeObject<List<object>>(value.ToString());
    }
}