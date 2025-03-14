$response = Invoke-WebRequest -Method POST -Uri "http://localhost:8080/realms/groundup/protocol/openid-connect/token" -ContentType "application/x-www-form-urlencoded" -Body "grant_type=password&client_id=groundup-api&username=robuser&password=!Password1&client_secret=OQWAiZF96XepMbFfD0mFRxsB0F3wQO1W" -UseBasicParsing

$tokenData = $response.Content | ConvertFrom-Json
$accessToken = $tokenData.access_token
Write-Host $accessToken