rem !! CHANGE THE VERSION !!
set "v=0.0.2"

pause

nuget push "Unturned.uTest.Compat\bin\Release\Unturned.uTest.Compat.%v%.nupkg" -Source https://api.nuget.org/v3/index.json -SkipDuplicate
nuget push "Unturned.uTest\bin\Release\Unturned.uTest.%v%.nupkg" -Source https://api.nuget.org/v3/index.json -SkipDuplicate
nuget push "Unturned.uTest.Runner\bin\Release\Unturned.uTest.Runner.%v%.nupkg" -Source https://api.nuget.org/v3/index.json -SkipDuplicate