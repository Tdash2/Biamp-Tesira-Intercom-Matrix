public class PartyLine
{
    public int Id { get; set; }
    public string Name { get; set; } = "";

    public List<int> Inputs { get; set; } = new();
    public List<int> Outputs { get; set; } = new();
}