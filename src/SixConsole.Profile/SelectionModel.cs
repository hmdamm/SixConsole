namespace SixConsole.Profile;

public enum NavigationDirection
{
    Next,
    Previous,
}

/// <summary>
/// All Lua navigation semantics exactly, but implemented purely as list operations on the operator
/// list and each operator's weapon list, so tests can cover them and the input engine stays thin.
/// </summary>
public sealed class SelectionModel
{
    private readonly IReadOnlyList<Operator> operators;
    private int operatorIndex;
    private int weaponIndex;

    public SelectionModel(IReadOnlyList<Operator> operators, int operatorIndex = 0, int weaponIndex = 0)
    {
        if (operators.Count == 0) throw new ArgumentException("Operator list must not be empty", nameof(operators));
        if (operatorIndex < 0 || operatorIndex >= operators.Count)
            throw new ArgumentOutOfRangeException(nameof(operatorIndex), "Operator index out of range");
        var op = operators[operatorIndex];
        if (weaponIndex < 0 || weaponIndex >= op.Weapons.Count)
            throw new ArgumentOutOfRangeException(nameof(weaponIndex), "Weapon index out of range");
        this.operators = operators;
        this.operatorIndex = operatorIndex;
        this.weaponIndex = weaponIndex;
    }

    public Operator Operator => operators[operatorIndex];

    public Weapon Weapon => Operator.Weapons[weaponIndex];

    public int OperatorIndex => operatorIndex;

    public int WeaponIndex => weaponIndex;

    /// <summary>Total operators in the catalogue (for display "i/n" parity with the source).</summary>
    public int OperatorCount => operators.Count;

    /// <summary>1-based index, straight from the current source's presentation.</summary>
    public int OperatorIndexOneBased => operatorIndex + 1;

    public int WeaponIndexOneBased => weaponIndex + 1;

    public event Action? Changed;

    public void NavigateOperator(NavigationDirection direction)
    {
        // Lua (1-based): next = (i % n) + 1 ; prev = (i - 2 + n) % n + 1
        operatorIndex = direction == NavigationDirection.Next
            ? (operatorIndex + 1) % operators.Count
            : (operatorIndex - 1 + operators.Count) % operators.Count;
        weaponIndex = 0; // source: currentGunIndex = 1
        Changed?.Invoke();
    }

    public void NavigateWeapon(NavigationDirection direction)
    {
        var count = Operator.Weapons.Count;
        weaponIndex = direction == NavigationDirection.Next
            ? (weaponIndex + 1) % count
            : (weaponIndex - 1 + count) % count;
        Changed?.Invoke();
    }

    public void SelectOperator(string operatorId)
    {
        int idx = -1;
        for (int i = 0; i < operators.Count; i++)
        {
            if (operators[i].Id == operatorId) { idx = i; break; }
        }
        if (idx < 0) throw new ArgumentOutOfRangeException(nameof(operatorId), $"Unknown operator {operatorId}");
        operatorIndex = idx;
        weaponIndex = 0;
        Changed?.Invoke();
    }

    public void SelectWeapon(Operator @operator, int weaponIndexOneBased)
    {
        if (@operator.Id != Operator.Id) throw new ArgumentException("Operator is not the selected one", nameof(@operator));
        if (weaponIndexOneBased < 1 || weaponIndexOneBased > @operator.Weapons.Count)
            throw new ArgumentOutOfRangeException(nameof(weaponIndexOneBased), "Weapon index out of range");
        weaponIndex = weaponIndexOneBased - 1;
        Changed?.Invoke();
    }
}
