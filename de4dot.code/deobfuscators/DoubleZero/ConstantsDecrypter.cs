using System;
using System.Collections.Generic;
using System.Text;

namespace de4dot.code.deobfuscators.DoubleZero {
	class ConstantsDecrypter {
		public static int DecryptIntegerFromBytearray(byte[] bytearray) {
			int[] integers = new int[bytearray.Length / 4];
			for (int i = 0; i < bytearray.Length / 4; i++) {
				// Converting every 4 bytes in the byte [] array to an integer, thus obtaining an int [] array
				integers[i] = bytearray[4 * i] | (bytearray[4 * i + 1] << 8) | (bytearray[4 * i + 2] << 16) | (bytearray[4 * i + 3] << 24);
			}
			// Turning the int [] array into an integer
			return DecryptInt(integers, 0, 0);
		}
		private static int DecryptInt(int[] a1, int a2, int a3) {
			// This function was copied and pasted from dnSpy
			int num = -1;
			int num2 = -1;
			int num3 = -1;
			int num4 = a2;
			int[] array = new int[a3];
			int[] array2 = new int[1000];
			double[] array3 = new double[500];
			int[] array4 = new int[1000];
			object[] array5 = new object[1000];
			int num5 = a1[num4];
			try {
				while (num5 != 180 && num4 < a1.Length) {
					try {
						num4++;
						if (num5 <= 120) {
							if (num5 <= 60) {
								if (num5 <= 32) {
									if (num5 != 10) {
										if (num5 == 20) {
											goto IL_42D;
										}
										switch (num5) {
										case 30:
											goto IL_471;
										case 31:
											goto IL_4B5;
										case 32:
											goto IL_4FD;
										default:
											goto IL_8B1;
										}
									}
								}
								else {
									if (num5 == 40) {
										goto IL_545;
									}
									if (num5 == 50) {
										goto IL_58E;
									}
									if (num5 != 60) {
										goto IL_8B1;
									}
									goto IL_5D7;
								}
							}
							else if (num5 <= 90) {
								if (num5 == 70) {
									goto IL_605;
								}
								if (num5 == 80) {
									goto IL_642;
								}
								if (num5 != 90) {
									goto IL_8B1;
								}
								goto IL_67E;
							}
							else {
								if (num5 == 100) {
									goto IL_6B2;
								}
								if (num5 == 110) {
									goto IL_6F4;
								}
								if (num5 != 120) {
									goto IL_8B1;
								}
								goto IL_72F;
							}
						}
						else {
							if (num5 <= 200) {
								if (num5 <= 160) {
									if (num5 == 130) {
										goto IL_771;
									}
									if (num5 == 150) {
										goto IL_7AC;
									}
									if (num5 != 160) {
										goto IL_8B1;
									}
									goto IL_7D3;
								}
								else {
									if (num5 == 170) {
										goto IL_88A;
									}
									if (num5 != 190) {
										if (num5 != 200) {
											goto IL_8B1;
										}
										goto IL_346;
									}
								}
							}
							else if (num5 <= 230) {
								if (num5 == 210) {
									goto IL_316;
								}
								if (num5 == 220) {
									goto IL_376;
								}
								if (num5 != 230) {
									goto IL_8B1;
								}
								goto IL_3AF;
							}
							else {
								if (num5 <= 250) {
									if (num5 == 240) {
										goto IL_29E;
									}
									if (num5 != 250) {
										goto IL_8B1;
									}
								}
								else {
									if (num5 != 260) {
										if (num5 != 270) {
											goto IL_8B1;
										}
										try {
											double num6 = array3[num2--];
											double num7 = array3[num2--];
											array3[++num2] = num7 - num6;
											goto IL_8D2;
										}
										catch (Exception ex) {
											if (!(ex.Message == "XL7Tg")) {
												throw ex;
											}
											goto IL_8D2;
										}
									}
									try {
										double num6 = array3[num2--];
										double num7 = array3[num2--];
										array3[++num2] = Math.Pow(num7, num6);
										goto IL_8D2;
									}
									catch (Exception ex2) {
										if (!(ex2.Message == "XL7Tg")) {
											throw ex2;
										}
										goto IL_8D2;
									}
								}
								try {
									double num6 = array3[num2--];
									double num7 = array3[num2--];
									array3[++num2] = num6 * num7;
									goto IL_8D2;
								}
								catch (Exception ex3) {
									if (!(ex3.Message == "XL7Tg")) {
										throw ex3;
									}
									goto IL_8D2;
								}
IL_29E:
								try {
									double num6 = array3[num2--];
									double num7 = array3[num2--];
									array3[++num2] = num6 + num7;
									goto IL_8D2;
								}
								catch (Exception ex4) {
									if (!(ex4.Message == "XL7Tg")) {
										throw ex4;
									}
									goto IL_8D2;
								}
							}
							try {
								array3[++num2] = (double)array2[num--];
								goto IL_8D2;
							}
							catch (Exception ex5) {
								if (!(ex5.Message == "XL7Tg")) {
									throw ex5;
								}
								goto IL_8D2;
							}
IL_316:
							try {
								array3[num2] = Math.Sin(array3[num2]);
								goto IL_8D2;
							}
							catch (Exception ex6) {
								if (!(ex6.Message == "XL7Tg")) {
									throw ex6;
								}
								goto IL_8D2;
							}
IL_346:
							try {
								array3[num2] = Math.Cos(array3[num2]);
								goto IL_8D2;
							}
							catch (Exception ex7) {
								if (!(ex7.Message == "XL7Tg")) {
									throw ex7;
								}
								goto IL_8D2;
							}
IL_376:
							try {
								array3[num2] = Math.Pow(array3[num2], 2.0);
								goto IL_8D2;
							}
							catch (Exception ex8) {
								if (!(ex8.Message == "XL7Tg")) {
									throw ex8;
								}
								goto IL_8D2;
							}
IL_3AF:
							try {
								array2[++num] = (int)Math.Round(array3[num2--], MidpointRounding.AwayFromZero);
								goto IL_8D2;
							}
							catch (Exception ex9) {
								if (!(ex9.Message == "XL7Tg")) {
									throw ex9;
								}
								goto IL_8D2;
							}
						}
						try {
							int num8 = array2[num--];
							int num9 = array2[num--];
							array2[++num] = num9 + num8;
							goto IL_8D2;
						}
						catch (Exception ex10) {
							if (!(ex10.Message == "XL7Tg")) {
								throw ex10;
							}
							goto IL_8D2;
						}
IL_42D:
						try {
							int num8 = array2[num--];
							int num9 = array2[num--];
							array2[++num] = num9 - num8;
							goto IL_8D2;
						}
						catch (Exception ex11) {
							if (!(ex11.Message == "XL7Tg")) {
								throw ex11;
							}
							goto IL_8D2;
						}
IL_471:
						try {
							int num8 = array2[num--];
							int num9 = array2[num--];
							array2[++num] = num9 * num8;
							goto IL_8D2;
						}
						catch (Exception ex12) {
							if (!(ex12.Message == "XL7Tg")) {
								throw ex12;
							}
							goto IL_8D2;
						}
IL_4B5:
						try {
							int num10 = a1[num4++];
							int num11 = array2[num--];
							array2[++num] = num11 << num10;
							goto IL_8D2;
						}
						catch (Exception ex13) {
							if (!(ex13.Message == "XL7Tg")) {
								throw ex13;
							}
							goto IL_8D2;
						}
IL_4FD:
						try {
							int num12 = a1[num4++];
							int num13 = array2[num--];
							array2[++num] = num13 >> num12;
							goto IL_8D2;
						}
						catch (Exception ex14) {
							if (!(ex14.Message == "XL7Tg")) {
								throw ex14;
							}
							goto IL_8D2;
						}
IL_545:
						try {
							int num8 = array2[num--];
							int num9 = array2[num--];
							array2[++num] = ((num9 < num8) ? 1 : 0);
							goto IL_8D2;
						}
						catch (Exception ex15) {
							if (!(ex15.Message == "XL7Tg")) {
								throw ex15;
							}
							goto IL_8D2;
						}
IL_58E:
						try {
							int num8 = array2[num--];
							int num9 = array2[num--];
							array2[++num] = ((num9 == num8) ? 1 : 0);
							goto IL_8D2;
						}
						catch (Exception ex16) {
							if (!(ex16.Message == "XL7Tg")) {
								throw ex16;
							}
							goto IL_8D2;
						}
IL_5D7:
						try {
							num4 = a1[num4++];
							goto IL_8D2;
						}
						catch (Exception ex17) {
							if (!(ex17.Message == "XL7Tg")) {
								throw ex17;
							}
							goto IL_8D2;
						}
IL_605:
						try {
							int num14 = a1[num4++];
							if (array2[num--] == 1) {
								num4 = num14;
							}
							goto IL_8D2;
						}
						catch (Exception ex18) {
							if (!(ex18.Message == "XL7Tg")) {
								throw ex18;
							}
							goto IL_8D2;
						}
IL_642:
						try {
							int num14 = a1[num4++];
							if (array2[num--] == 0) {
								num4 = num14;
							}
							goto IL_8D2;
						}
						catch (Exception ex19) {
							if (!(ex19.Message == "XL7Tg")) {
								throw ex19;
							}
							goto IL_8D2;
						}
IL_67E:
						try {
							array2[++num] = a1[num4++];
							goto IL_8D2;
						}
						catch (Exception ex20) {
							if (!(ex20.Message == "XL7Tg")) {
								throw ex20;
							}
							goto IL_8D2;
						}
IL_6B2:
						try {
							int num15 = a1[num4++];
							array2[++num] = ((int[])array5[num3])[num15];
							goto IL_8D2;
						}
						catch (Exception ex21) {
							if (!(ex21.Message == "XL7Tg")) {
								throw ex21;
							}
							goto IL_8D2;
						}
IL_6F4:
						try {
							int num14 = a1[num4++];
							array2[++num] = array[num14];
							goto IL_8D2;
						}
						catch (Exception ex22) {
							if (!(ex22.Message == "XL7Tg")) {
								throw ex22;
							}
							goto IL_8D2;
						}
IL_72F:
						try {
							int num15 = a1[num4++];
							((int[])array5[num3])[num15] = array2[num--];
							goto IL_8D2;
						}
						catch (Exception ex23) {
							if (!(ex23.Message == "XL7Tg")) {
								throw ex23;
							}
							goto IL_8D2;
						}
IL_771:
						try {
							int num14 = a1[num4++];
							array[num14] = array2[num--];
							goto IL_8D2;
						}
						catch (Exception ex24) {
							if (!(ex24.Message == "XL7Tg")) {
								throw ex24;
							}
							goto IL_8D2;
						}
IL_7AC:
						try {
							num--;
							goto IL_8D2;
						}
						catch (Exception ex25) {
							if (!(ex25.Message == "XL7Tg")) {
								throw ex25;
							}
							goto IL_8D2;
						}
IL_7D3:
						try {
							int num14 = a1[num4++];
							int num16 = a1[num4++];
							array5[++num3] = new int[num16];
							array4[num3] = num4;
							int num17 = num - num16 + 1;
							try {
								for (int i = 0; i < num16; i++) {
									try {
										((int[])array5[num3])[i] = array2[num17 + i];
									}
									catch (Exception ex26) {
										if (ex26.Message != "ZLKvn1phF") {
											throw ex26;
										}
									}
								}
							}
							catch (Exception ex27) {
								if (ex27.Message != "sgKi4DZ") {
									throw ex27;
								}
							}
							num -= num16;
							num4 = num14;
							goto IL_8D2;
						}
						catch (Exception ex28) {
							if (!(ex28.Message == "XL7Tg")) {
								throw ex28;
							}
							goto IL_8D2;
						}
IL_88A:
						try {
							num4 = array4[num3--];
							goto IL_8D2;
						}
						catch (Exception ex29) {
							if (!(ex29.Message == "XL7Tg")) {
								throw ex29;
							}
							goto IL_8D2;
						}
IL_8B1:
						try {
							throw new Exception();
						}
						catch (Exception ex30) {
							if (!(ex30.Message == "XL7Tg")) {
								throw ex30;
							}
						}
IL_8D2:
						num5 = a1[num4];
					}
					catch (Exception ex31) {
						if (ex31.Message != "ZLKvn1phF") {
							throw ex31;
						}
					}
				}
			}
			catch (Exception ex32) {
				if (ex32.Message != "sgKi4DZ") {
					throw ex32;
				}
			}
			return array2[num];
		}
	}
}
