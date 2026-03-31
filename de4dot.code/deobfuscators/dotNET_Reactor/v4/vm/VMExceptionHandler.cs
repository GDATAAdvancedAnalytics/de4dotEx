/*
    Copyright (C) 2011-2020 de4dot@gmail.com
                  2025-2026 G DATA Advanced Analytics GmbH

    This file is part of de4dotEx.

    de4dotEx is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    de4dotEx is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with de4dotEx.  If not, see <http://www.gnu.org/licenses/>.
*/

using dnlib.DotNet.Emit;

namespace de4dot.code.deobfuscators.dotNET_Reactor.v4.vm;

/**
 * Represents exception handlers in virtualized methods. Pretty much the same as the
 * real deal, except that instructions/tokens are not resolved yet.
 */
internal class VMExceptionHandler {
	public int TryStart { get; set; }
	public int TryEnd { get; set; }  // Seems to point to the last instruction inside the block instead of next one outside
	public int HandlerStart  { get; set; }
	public int HandlerEnd { get; set; }  // See TryEnd
	public ExceptionHandlerType HandlerType { get; set; }

	public int FilterStart { get; set; }
	public int CatchType { get; set; }  // TypeRef token
}
