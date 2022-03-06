namespace DbDeploy;

public class CommandLineOptions
{
    public string search_path { get; set; } = String.Empty;
    public string connection_string { get; set; } = String.Empty;
    public int lower_id { get; set; } = 1;
    public int upper_id { get; set; } = int.MaxValue;
}
