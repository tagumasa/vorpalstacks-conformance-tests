run "create_api" {
  command = apply

  assert {
    condition     = aws_api_gateway_rest_api.basic.name == "tf-test-api"
    error_message = "API name should be tf-test-api"
  }

  assert {
    condition     = can(regex("arn:aws:apigateway:", aws_api_gateway_rest_api.basic.arn))
    error_message = "API ARN should be a valid API Gateway ARN"
  }
}

run "create_stage" {
  command = apply

  assert {
    condition     = aws_api_gateway_stage.prod.stage_name == "prod"
    error_message = "Stage name should be prod"
  }

  assert {
    condition     = aws_api_gateway_stage.prod.deployment_id == aws_api_gateway_deployment.basic.id
    error_message = "Stage should reference the deployment"
  }
}
