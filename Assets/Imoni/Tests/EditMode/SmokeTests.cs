using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Imoni.Core.Tests
{
    public class SmokeTests
    {
        [Test]
        public void GameConfigJsonIsAvailableAndReadable()
        {
            // One test source runs in both Unity EditMode and the Unity-free dotnet host.
#if UNITY_EDITOR
            var path = Path.Combine(UnityEngine.Application.dataPath, "Imoni", "Config", "GameConfig.json");
#else
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "GameConfig.json");
#endif
            Assert.That(File.Exists(path), Is.True, path);
            var config = JObject.Parse(File.ReadAllText(path));
            Assert.That(config["version"]?.Type, Is.EqualTo(JTokenType.Integer));
        }
    }
}
