﻿// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Threading;
using Nerdbank.Streams;
using Xunit;

namespace MessagePack.Tests
{
    public class MessagePackWriterTests
    {
        /// <summary>
        /// Verifies that <see cref="MessagePackWriter.WriteRaw(ReadOnlySpan{byte})"/>
        /// accepts a span that came from stackalloc.
        /// </summary>
        [Fact]
        public unsafe void WriteRaw_StackAllocatedSpan()
        {
            var sequence = new Sequence<byte>();
            var writer = new MessagePackWriter(sequence);

            Span<byte> bytes = stackalloc byte[8];
            bytes[0] = 1;
            bytes[7] = 2;
            fixed (byte* pBytes = bytes)
            {
                var flexSpan = new Span<byte>(pBytes, bytes.Length);
                writer.WriteRaw(flexSpan);
            }

            writer.Flush();
            var written = sequence.AsReadOnlySequence.ToArray();
            Assert.Equal(1, written[0]);
            Assert.Equal(2, written[7]);
        }

        [Fact]
        public void Write_ByteArray_null()
        {
            var sequence = new Sequence<byte>();
            var writer = new MessagePackWriter(sequence);
            writer.Write((byte[])null);
            writer.Flush();
            var reader = new MessagePackReader(sequence.AsReadOnlySequence);
            Assert.True(reader.TryReadNil());
        }

        [Fact]
        public void Write_ByteArray()
        {
            var sequence = new Sequence<byte>();
            var writer = new MessagePackWriter(sequence);
            var buffer = new byte[] { 1, 2, 3 };
            writer.Write(buffer);
            writer.Flush();
            var reader = new MessagePackReader(sequence.AsReadOnlySequence);
            Assert.Equal(buffer, reader.ReadBytes().Value.ToArray());
        }

        [Fact]
        public void Write_String_null()
        {
            var sequence = new Sequence<byte>();
            var writer = new MessagePackWriter(sequence);
            writer.Write((string)null);
            writer.Flush();
            var reader = new MessagePackReader(sequence.AsReadOnlySequence);
            Assert.True(reader.TryReadNil());
        }

        [Fact]
        public void Write_String()
        {
            var sequence = new Sequence<byte>();
            var writer = new MessagePackWriter(sequence);
            string expected = "hello";
            writer.Write(expected);
            writer.Flush();
            var reader = new MessagePackReader(sequence.AsReadOnlySequence);
            Assert.Equal(expected, reader.ReadString());
        }

        [Fact]
        public void WriteExtensionFormatHeader_NegativeExtension()
        {
            var sequence = new Sequence<byte>();
            var writer = new MessagePackWriter(sequence);

            var header = new ExtensionHeader(-1, 10);
            writer.WriteExtensionFormatHeader(header);
            writer.Flush();

            var written = sequence.AsReadOnlySequence;
            var reader = new MessagePackReader(written);
            var readHeader = reader.ReadExtensionFormatHeader();

            Assert.Equal(header.TypeCode, readHeader.TypeCode);
            Assert.Equal(header.Length, readHeader.Length);
        }

        [Fact]
        public void CancellationToken()
        {
            var sequence = new Sequence<byte>();
            var writer = new MessagePackWriter(sequence);
            Assert.False(writer.CancellationToken.CanBeCanceled);

            var cts = new CancellationTokenSource();
            writer.CancellationToken = cts.Token;
            Assert.Equal(cts.Token, writer.CancellationToken);
        }

        [Fact]
        public void TryWriteWithBuggyWriter()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                var writer = new MessagePackWriter(new BuggyBufferWriter());
                writer.WriteRaw(new byte[10]);
            });
        }

        /// <summary>
        /// Besides being effectively a no-op, this <see cref="IBufferWriter{T}"/>
        /// is buggy because it can return empty arrays, which should never happen.
        /// A sizeHint=0 means give me whatever memory is available, but should never be empty.
        /// </summary>
        private class BuggyBufferWriter : IBufferWriter<byte>
        {
            public void Advance(int count)
            {
            }

            public Memory<byte> GetMemory(int sizeHint = 0) => new byte[sizeHint];

            public Span<byte> GetSpan(int sizeHint = 0) => new byte[sizeHint];
        }
    }
}
