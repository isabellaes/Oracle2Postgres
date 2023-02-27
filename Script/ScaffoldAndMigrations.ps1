dotnet user-secrets init
dotnet user-secrets set ConnectionStrings:Oracle "put your oracle connectionstring here"
dotnet user-secrets set ConnectionStrings:Postgres "put your postgres connectionstring here"

dotnet ef dbcontext scaffold Name=ConnectionStrings:Oracle Oracle.EntityFrameworkCore -o Models -c ModelContext  --context-dir Context --force

$file = @(Get-ChildItem ./ModelContext.cs)

if($file)
{
    (Get-Content $_).Replace('ConnectionStrings:Oracle', 'ConnectionStrings:Postgres') | Set-Content $_
}

dotnet ef migrations add migration

$files = @(Get-ChildItem ./Migrations/*.cs)
foreach ($file in $files) 
{
    (Get-Content $_).Replace('"NUMBER"', '"NUMERIC"') | Set-Content $_
    (Get-Content $_).Replace('"NUMBER(38)"', '"NUMERIC(38)"') | Set-Content $_
    (Get-Content $_).Replace("SYSDATE", "CURRENT_TIMESTAMP") | Set-Content $_
    (Get-Content $_).Replace("sysdate ", "CURRENT_TIMESTAMP") | Set-Content $_
    (Get-Content $_).Replace("TO_CHAR(RAWTOHEX(SYS_GUID()))", "gen_random_uuid()") | Set-Content $_
    (Get-Content $_).Replace("to_char(rawtohex(sys_guid()))", "gen_random_uuid()") | Set-Content $_
    (Get-Content $_).Replace("CLOB", "TEXT") | Set-Content $_
    
}

$files = @(Get-ChildItem ./Models/*.cs)
foreach ($file in $files) 
{
    (Get-Content $file).replace('byte', 'int')| Set-Content $file
}

dotnet ef database update 
