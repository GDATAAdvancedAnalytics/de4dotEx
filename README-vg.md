## de4dot VG
A de4dot fork with full support for [VirtualGuard Protector](https://virtualguard.io/)

*Refer to the blogpost for more information.*    
https://mrt4ntr4.github.io/VirtualGuard-P1/

## Features
* Fixes control flow
* Fixes proxy calls
* Removes Anti-Debug methods
* Removes Anti-Tamper methods

---

## Samples

### Before (obfuscated):
```csharp
9.6 <<EMPTY_NAME>> = new 9.6();
byte[] buffer;
Assembly assembly;
for (;;)
{
	IL_06:
	uint num = 0x6FB11A9DU;
	for (;;)
	{
		uint num2;
		switch ((num2 = (num ^ 0x7C4718BDU)) % 0xAU)
		{
		case 0U:
		{
			uint num3 = num2;
			uint[] array = new uint[4];
			array[0] = 0x17FU;
			array[1] = 5U + array[0];
			array[2] = 0x12DU - array[1] + array[0];
			array[3] = 0x2E6U - array[2] + array[1] - array[0];
			uint num4 = num3 / array[3];
			num = num4 - 0xC4EFA7F5U;
			continue;
		}
		case 1U:
		{
			bool flag;
			num = (((!flag) ? 0x2FB1F33CU : 0x78195D88U) ^ num2 / 0x69CU);
			continue;
		}
		case 2U:
		{
			buffer = new byte[2];
			uint num5 = num2;
			uint[] array = new uint[3];
			array[0] = 0x106U;
			array[1] = 0x29FU - array[0];
			array[2] = 0x15AU + array[1] - array[0];
			uint num6 = num5 / array[2];
			num = num6 - 0xA952A5ABU;
			continue;
		}
		case 3U:
			goto IL_06;
		case 4U:
		{
			<<EMPTY_NAME>>.0e. = A_0;
			uint num7 = num2;
			uint[] array = new uint[3];
			array[0] = 0x6DU;
			array[1] = 0x10DU - array[0];
			array[2] = 0x22FU - array[1] - array[0];
			uint num8 = num7 / array[2];
			num = num8 - 0x865CCA94U;
			continue;
		}
		case 5U:
			goto IL_22E;
		case 6U:
		{
			assembly = ed.f3();
			uint num9 = num2;
			uint[] array = new uint[4];
			array[0] = 0x1E5U;
			array[1] = 0xFFFFFF2AU + array[0];
			array[2] = 0xFFFFFE5BU + array[1] + array[0];
			array[3] = 0x361U + array[2] - array[1] - array[0];
			uint num10 = num9 / array[3];
			num = num10 - 0xB8623758U;
			continue;
		}
		case 7U:
		{
			bool flag = 9.ab1.;
			uint num11 = num2;
			uint[] array = new uint[4];
			array[0] = 0xDDU;
			array[1] = 0x47U + array[0];
			array[2] = 0x3B5U - array[1] - array[0];
			array[3] = 0xBDU + array[2] - array[1] - array[0];
			uint num12 = num11 / array[3];
			num = num12 - 0xD8E918C6U;
			continue;
		}
		case 9U:
			9.cb2. = new Dictionary<int, 9.85>();
			num = 0x3FBD817DU;
			continue;
		}
		goto Block_1;
	}
}
```

### After:
```csharp
9.6 <<EMPTY_NAME>> = new 9.6();
<<EMPTY_NAME>>.0e. = A_0;
if (!9.ab1.)
{
	9.cb2. = new Dictionary<int, 9.85>();
	byte[] buffer = new byte[2];
	Assembly executingAssembly = Assembly.GetExecutingAssembly();
```
