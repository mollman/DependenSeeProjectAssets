namespace DependenSee;

public static class Extensions
{

    public static bool AddIfNotExists(this List<Package> list, Package item)
    {
        if (!list.Any(p => p.Id == item.Id))
        {
            list.Add(item);
            return true;
        }
        return false;
    }

    public static bool AddIfNotExists(this List<Project> list, Project item)
    {
        if (!list.Any(p => p.Id == item.Id))
        {
            list.Add(item);
            return true;
        }
        return false;
    }

    public static bool AddIfNotExists(this List<Reference> list, Reference item)
    {
        if (!list.Any(p => p.From == item.From && p.To == item.To) )
        {
            list.Add(item);
            return true;
        }
        return false;
    }
}
