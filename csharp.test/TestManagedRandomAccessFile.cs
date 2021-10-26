using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ParquetSharp.IO;

namespace ParquetSharp.Test
{
    [TestFixture]
    internal static class TestManagedRandomAccessFile
    {
        
        [Test]
        public static void TestReadNestedList()
        {
            var directory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            var path = Path.Combine(directory!, "TestFiles/sample_nested.parquet");

            using var fileReader = new ParquetFileReader(path);
            var rowGroupReader = fileReader.RowGroup(0);

            // some_id
            var column0 = rowGroupReader.Column(0);
            Console.WriteLine();
            Console.WriteLine(column0.ColumnDescriptor.Path);
            Console.WriteLine("--------------------------------------");

            var column0Reader = column0.LogicalReader<string?>();
            var contents0 = column0Reader.ReadAll(2);
            Assert.IsNotEmpty(contents0);
            TestContext.Out.WriteLine(string.Join("\n", contents0));

            // struct.list.item.string_in_struct
            var column1 = rowGroupReader.Column(1);
            Console.WriteLine();
            Console.WriteLine(column1.ColumnDescriptor.Path);
            Console.WriteLine("--------------------------------------");

            var column1Reader = column1.LogicalReader<long?[]>();
            var contents1 = column1Reader.ReadAll(2);
            Assert.IsNotEmpty(contents1);

            // struct.list.item.array.list.item
            var column2 = rowGroupReader.Column(2);
            Console.WriteLine();
            Console.WriteLine(column2.ColumnDescriptor.Path);
            Console.WriteLine("--------------------------------------");

            var column2Reader = column2.LogicalReader<long?[][]>();
            var contents2 = column2Reader.ReadAll(2);
            Assert.IsNotEmpty(contents2);

             // struct.list.item.string_in_struct
            var column3 = rowGroupReader.Column(3);
            Console.WriteLine();
            Console.WriteLine(column3.ColumnDescriptor.Path);
            Console.WriteLine("--------------------------------------");

            var column3Reader = column3.LogicalReader<string[]>();
            var contents3 = column3Reader.ReadAll(2);
            Assert.IsNotEmpty(contents3);
        }

        
        [Test]
        public static void TestInMemoryRoundTrip()
        {
            var expected = Enumerable.Range(0, 1024 * 1024).ToArray();
            using var buffer = new MemoryStream();

            // Write test data.
            using (var output = new ManagedOutputStream(buffer, leaveOpen: true))
            {
                using var writer = new ParquetFileWriter(output, new Column[] {new Column<int>("ids")});
                using var groupWriter = writer.AppendRowGroup();
                using var columnWriter = groupWriter.NextColumn().LogicalWriter<int>();

                columnWriter.WriteBatch(expected);

                writer.Close();
            }

            // Seek back to start.
            buffer.Seek(0, SeekOrigin.Begin);

            // Read test data.
            using var input = new ManagedRandomAccessFile(buffer, leaveOpen: true);
            using var reader = new ParquetFileReader(input);
            using var groupReader = reader.RowGroup(0);
            using var columnReader = groupReader.Column(0).LogicalReader<int>();

            Assert.AreEqual(expected, columnReader.ReadAll(expected.Length));
        }

        [Test]
        public static void TestFileStreamRoundTrip()
        {
            try
            {
                using (var output = new ManagedOutputStream(File.OpenWrite("file.parquet")))
                {
                    using var writer = new ParquetFileWriter(output, new Column[] {new Column<int>("ids")});
                    using var groupWriter = writer.AppendRowGroup();
                    using var columnWriter = groupWriter.NextColumn().LogicalWriter<int>();

                    columnWriter.WriteBatch(new[] {1, 2, 3});

                    writer.Close();
                }

                using var input = new ManagedRandomAccessFile(File.OpenRead("file.parquet"));
                using var reader = new ParquetFileReader(input);
                using var groupReader = reader.RowGroup(0);
                using var columnReader = groupReader.Column(0).LogicalReader<int>();

                Assert.AreEqual(new[] {1, 2, 3}, columnReader.ReadAll(3));
            }
            finally
            {
                File.Delete("file.parquet");
            }
        }

        [Test]
        public static void TestWriteException()
        {
            var exception = Assert.Throws<ParquetException>(() =>
            {
                using (var buffer = new ErroneousWriterStream())
                using (var output = new ManagedOutputStream(buffer))
                using (new ParquetFileWriter(output, new Column[] {new Column<int>("ids")}))
                {
                }
            });

            Assert.That(
                exception?.Message,
                Contains.Substring("this is an erroneous writer"));
        }

        [Test]
        public static void TestReadExeption()
        {
            var expected = Enumerable.Range(0, 1024 * 1024).ToArray();

            var exception = Assert.Throws<ParquetException>(() =>
            {
                using var buffer = new ErroneousReaderStream();

                using (var output = new ManagedOutputStream(buffer, leaveOpen: true))
                {
                    using var writer = new ParquetFileWriter(output, new Column[] {new Column<int>("ids")});
                    using var groupWriter = writer.AppendRowGroup();
                    using var columnWriter = groupWriter.NextColumn().LogicalWriter<int>();

                    columnWriter.WriteBatch(expected);

                    writer.Close();
                }

                buffer.Seek(0, SeekOrigin.Begin);

                using var input = new ManagedRandomAccessFile(buffer);
                using (new ParquetFileReader(input))
                {

                }
            });

            Assert.That(
                exception?.Message,
                Contains.Substring("this is an erroneous reader"));
        }

        private sealed class ErroneousReaderStream : MemoryStream
        {
            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new IOException("this is an erroneous reader");
            }
        }

        private sealed class ErroneousWriterStream : MemoryStream
        {
            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new IOException("this is an erroneous writer");
            }
        }
    }
}
