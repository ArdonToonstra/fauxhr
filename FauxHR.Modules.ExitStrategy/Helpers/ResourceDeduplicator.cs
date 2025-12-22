using Hl7.Fhir.Model;

namespace FauxHR.Modules.ExitStrategy.Helpers;

public static class ResourceDeduplicator
{
    /// <summary>
    /// Dedupes a list of resources.
    /// Resources are considered duplicates if they share ANY identifier (System + Value).
    /// Returns the version with the most recent Meta.LastUpdated for each unique entity.
    /// </summary>
    public static List<T> Deduplicate<T>(IEnumerable<T> resources, Func<T, IEnumerable<Identifier>> idSelector) where T : DomainResource
    {
        var inputList = resources.ToList();
        if (inputList.Count <= 1) return inputList;

        // We will build connected components.
        // Each resource is a node. An edge exists if they share an identifier.
        // We'll use a Union-Find (Disjoint Set) data structure or simple BFS/DFS to group them.
        
        // Map: "System|Value" -> List of resource indices that have this identifier
        var identifierMap = new Dictionary<string, List<int>>();

        for (int i = 0; i < inputList.Count; i++)
        {
            var r = inputList[i];
            var identifiers = idSelector(r);
            if (identifiers != null)
            {
                foreach (var id in identifiers)
                {
                    if (!string.IsNullOrEmpty(id.System) && !string.IsNullOrEmpty(id.Value))
                    {
                        var key = $"{id.System}|{id.Value}";
                        if (!identifierMap.ContainsKey(key))
                        {
                            identifierMap[key] = new List<int>();
                        }
                        identifierMap[key].Add(i);
                    }
                }
            }
        }

        // Adjacency list: index -> connected indices
        var adjacency = new List<int>[inputList.Count];
        for (int i = 0; i < inputList.Count; i++) adjacency[i] = new List<int>();

        foreach (var kvp in identifierMap)
        {
            var indices = kvp.Value;
            if (indices.Count > 1)
            {
                for (int i = 0; i < indices.Count - 1; i++)
                {
                    var u = indices[i];
                    var v = indices[i + 1];
                    adjacency[u].Add(v);
                    adjacency[v].Add(u);
                }
            }
        }

        // Find connected components
        var visited = new bool[inputList.Count];
        var result = new List<T>();

        for (int i = 0; i < inputList.Count; i++)
        {
            if (!visited[i])
            {
                // Start a new component
                var componentIndices = new List<int>();
                var queue = new Queue<int>();
                
                visited[i] = true;
                queue.Enqueue(i);
                
                while (queue.Count > 0)
                {
                    var curr = queue.Dequeue();
                    componentIndices.Add(curr);

                    foreach (var neighbor in adjacency[curr])
                    {
                        if (!visited[neighbor])
                        {
                            visited[neighbor] = true;
                            queue.Enqueue(neighbor);
                        }
                    }
                }

                // For this component, pick the "best" resource
                var best = PickBest(componentIndices.Select(idx => inputList[idx]));
                result.Add(best);
            }
        }

        return result;
    }

    private static T PickBest<T>(IEnumerable<T> candidates) where T : DomainResource
    {
        // Order by LastUpdated descending.
        // If LastUpdated matches or is null, maybe fallback to ID or something stable, 
        // but generally LastUpdated is what we want.
        return candidates
            .OrderByDescending(r => r.Meta?.LastUpdated ?? DateTimeOffset.MinValue)
            .First();
    }
}
