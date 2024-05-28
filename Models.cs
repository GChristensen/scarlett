
namespace Scarlett;

public record ActionNoun(
    string Action,
    string? Description = null,
    bool Confirm = false,
    bool Restrict = false,
    bool Disabled = false,
    Dictionary<string, object>? Args = null
    );
    
public record Restrictions (
    List<string?>? Processes
    );