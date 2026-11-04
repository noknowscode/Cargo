using NUnit.Framework;

// The screens format numbers in the machine's culture (a French machine reads "14,2 m draft"), so the tests that
// assert exact sentences run in one culture, whatever the machine's is. CultureTests checks the display follows it.
[assembly: SetCulture("en-US")]
[assembly: SetUICulture("en-US")]
