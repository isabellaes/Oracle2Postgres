
public class PkEqualityComparer : IEqualityComparer<Object>
{
    List<string> pks;
    public PkEqualityComparer(List<string> pks)
    {
        this.pks = pks;
    }

    new public bool Equals(Object p1, Object p2)
    {
        foreach (var pk in pks)
        {
            var equal = p1.GetType().GetProperty(pk).GetValue(p1).ToString() == p2.GetType().GetProperty(pk).GetValue(p2).ToString();
            if (!equal)
            {
                return false;
            }
        }
        return true;
    }

    public int GetHashCode(Object p1)
    {
        if (p1 == null)
            return -1;
        int hash = 1;
        // Suitable nullity checks etc, of course 🙂
        foreach (var pk in pks)
        {
            hash = hash * 23 + p1.GetType().GetProperty(pk).GetValue(p1).GetHashCode();
        }

        return hash;
    }
}