using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeuroSpeech.Eternity.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace NeuroSpeech.Eternity.Tests
{
    [TestClass]
    public class TupleTests
    {

        [TestMethod]
        public void Test()
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ValueTupleConverter());

            var text = JsonSerializer.Serialize((5, true, 2.0), options);
            var (five, @true, two) = JsonSerializer.Deserialize<(int, bool, double)>(text, options);

            Assert.AreEqual(five, 5);
            Assert.AreEqual(@true, true);
            Assert.AreEqual(two, 2.0);
        }

    }
}
