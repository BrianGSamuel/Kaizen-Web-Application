namespace KaizenWebApp.ViewModels
{
    public class UpdateMarkingCriteriaRequest
    {
        public int Id { get; set; }
        public string CriteriaName { get; set; } = string.Empty;
        public int Weight { get; set; }
    }
}
