// Run on the IIS machine. Assign its Jenkins agent the label windows-iis.
// The agent needs Git, the .NET 10 SDK, Node.js, and permission to manage IIS.
def runCiCommand(String command) {
    if (isUnix()) {
        sh command
    } else {
        bat command
    }
}

pipeline {
    agent { label 'windows-iis' }

    options {
        skipDefaultCheckout()
        disableConcurrentBuilds()
        timeout(time: 20, unit: 'MINUTES')
        buildDiscarder(logRotator(numToKeepStr: '20', artifactNumToKeepStr: '5'))
    }

    // Works behind a firewall too. Run Build Now once to register this trigger.
    triggers {
        pollSCM('* * * * *')
    }

    parameters {
        string(name: 'IIS_APP_POOL', defaultValue: '', description: 'Leave blank to find the dedicated App Pool from the IIS site physical path.')
        string(name: 'IIS_HEALTH_URL', defaultValue: 'http://myapp.test/Employees', description: 'Health check URL returning HTTP 200. Failure rolls back the files.')
    }

    environment {
        DOTNET_CLI_TELEMETRY_OPTOUT = '1'
        DOTNET_NOLOGO = '1'
        DOTNET_CLI_HOME = "${WORKSPACE}/.local/dotnet"
        NUGET_PACKAGES = "${WORKSPACE}/.local/packages"
        IIS_DEPLOY_PATH = 'C:\\inetpub\\wwwroot\\test-deploy'
        IIS_HEALTH_CHECK_URL = "${params.IIS_HEALTH_URL ?: 'http://myapp.test/Employees'}"
    }

    stages {
        stage('Checkout') {
            steps {
                script {
                    def revision = checkout scm
                    def sourceBranch = env.BRANCH_NAME ?: revision.GIT_BRANCH ?: ''
                    env.DEPLOY_TEST = (!env.CHANGE_ID && sourceBranch in ['test', 'origin/test', 'refs/heads/test', 'refs/remotes/origin/test']) ? 'true' : 'false'
                }
            }
        }

        stage('Check tools') {
            steps {
                script {
                    runCiCommand('dotnet --version')
                    runCiCommand('node --version')
                }
            }
        }

        stage('Restore') {
            steps {
                script {
                    runCiCommand('dotnet restore EmployeeManagement.sln')
                }
            }
        }

        stage('Build') {
            steps {
                script {
                    runCiCommand('dotnet build EmployeeManagement.sln --configuration Release --no-restore')
                }
            }
        }

        stage('Test') {
            steps {
                script {
                    // Checks is a console executable, so use dotnet run, not dotnet test.
                    runCiCommand('dotnet run --project tests/EmployeeManagement.Checks --configuration Release --no-build --no-restore')
                    runCiCommand('node --check < EmployeeManagement/wwwroot/js/site.js')
                }
            }
        }

        stage('Publish artifact') {
            steps {
                // Remove old publish files before collecting this build's output.
                dir('artifacts/publish') {
                    deleteDir()
                }
                script {
                    runCiCommand('dotnet publish EmployeeManagement/EmployeeManagement.csproj --configuration Release --no-build --no-restore --output artifacts/publish')
                }
                archiveArtifacts artifacts: 'artifacts/publish/**', fingerprint: true
            }
        }

        stage('Deploy test to IIS') {
            when {
                expression { env.DEPLOY_TEST == 'true' }
            }
            steps {
                powershell '''
                    $ErrorActionPreference = 'Stop'
                    & "$env:WORKSPACE/scripts/deploy-iis.ps1" `
                        -PublishPath "$env:WORKSPACE/artifacts/publish" `
                        -DeployPath $env:IIS_DEPLOY_PATH `
                        -AppPoolName $env:IIS_APP_POOL `
                        -HealthCheckUrl $env:IIS_HEALTH_CHECK_URL
                '''
            }
        }
    }
}
