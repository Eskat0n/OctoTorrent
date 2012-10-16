Framework '4.0'

properties {	
	$buildNumber = $null
    $configuration = $null

    $solutionPath = '../'
	$solutionFilename = $solutionPath + 'OctoTorrent.sln'
}

task default -depends DoNotRunItDirectly

task DoNotRunItDirectly {
	'Do not run it directly!'
}

task CommonBuild -depends EnsureParams, Build

task EnsureParams {
	Assert ($buildNumber -ne $null) 'Property $buildNumber should be specified'
	Assert ($configuration -ne $null) 'Property $configuration should be specified'
}

task Build {
    MSBuild -t:Build -p:Configuration=$configuration $solutionFilename  
}