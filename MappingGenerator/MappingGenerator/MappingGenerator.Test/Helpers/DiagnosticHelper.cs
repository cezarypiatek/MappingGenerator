using TestHelper;

namespace MappingGenerator.Test.Helpers
{
    public static class DiagnosticHelper
    {
        public static DiagnosticResultLocation[] LocationFromTestFile(int row, int column)
        {
            return new[] {new DiagnosticResultLocation("Test0.cs", row, column)};
        }
    }
}
