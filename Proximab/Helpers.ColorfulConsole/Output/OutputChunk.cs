﻿using System;

namespace Helpers.ColorfulConsole.Output
{
    /// <summary>
    /// Represents information about the output chunk.
    /// </summary>
    public class OutputChunk
    {
        /// <summary>
        /// Gets the color in which the text will be displayed.
        /// </summary>
        public ConsoleColor Color { get; }

        /// <summary>
        /// Gets the text to display.
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OutputChunk"/> class.
        /// </summary>
        /// <param name="color">The color of the chunk</param>
        /// <param name="text">The chunk content</param>
        public OutputChunk(ConsoleColor color, string text)
        {
            Color = color;
            Text = text;
        }
    }
}
