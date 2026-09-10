{{- define "dorado-cloud.apiImage" -}}
{{- $tag := default .Chart.AppVersion .Values.image.tag -}}
{{ .Values.image.registry }}/{{ .Values.image.repository }}:{{ $tag }}
{{- end -}}

{{- define "dorado-cloud.gatewayImage" -}}
{{- $tag := default .Chart.AppVersion .Values.gateway.image.tag -}}
{{ .Values.gateway.image.registry }}/{{ .Values.gateway.image.repository }}:{{ $tag }}
{{- end -}}

{{- define "dorado-cloud.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{- define "dorado-cloud.labels" -}}
app.kubernetes.io/name: {{ include "dorado-cloud.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
helm.sh/chart: {{ .Chart.Name }}-{{ .Chart.Version }}
{{- end -}}

{{- define "dorado-cloud.postgresSecret" -}}
{{- if .Values.postgres.existingSecret -}}{{ .Values.postgres.existingSecret }}{{- else -}}{{ .Release.Name }}-postgres{{- end -}}
{{- end -}}
