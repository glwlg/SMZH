using NUnit.Framework;
using XTD.Content;
using XTD.Flow;

namespace XTD.Tests
{
    public sealed class MvpValidationServiceTests
    {
        [Test]
        public void Validate_GameContentPassesMvpScope()
        {
            var catalog = GameContentFactory.CreateCatalog();
            var report = MvpValidationService.Validate(catalog);

            Assert.That(report.Passed, Is.True, report.ToString());
        }
    }
}
