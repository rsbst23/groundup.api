$response = Invoke-WebRequest -Method POST -Uri "http://localhost:8080/realms/groundup/protocol/openid-connect/token" -ContentType "application/x-www-form-urlencoded" -Body "grant_type=password&client_id=groundup-api&username=robuser&password=!Password1&client_secret=OQWAiZF96XepMbFfD0mFRxsB0F3wQO1W" -UseBasicParsing

$tokenData = $response.Content | ConvertFrom-Json
$accessToken = $tokenData.access_token
Write-Host $accessToken


# $body = @{
    # client_id = "admin-cli"
    # client_secret = "GY9ReDEYV2cTNh6CZ1qfDH43EOIGCcQO"
    # grant_type = "client_credentials"
# }

# Invoke-RestMethod -Method Post -Uri "http://localhost:8080/realms/master/protocol/openid-connect/token" -Body $body -ContentType "application/x-www-form-urlencoded"