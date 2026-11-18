data "aws_eks_cluster" "aplicacao" {
  count = var.aplicacao_habilitada ? 1 : 0
  name  = var.aplicacao_cluster_name
}

data "aws_eks_cluster_auth" "aplicacao" {
  count = var.aplicacao_habilitada ? 1 : 0
  name  = var.aplicacao_cluster_name
}

provider "kubernetes" {
  host                   = var.aplicacao_habilitada ? data.aws_eks_cluster.aplicacao[0].endpoint : "https://127.0.0.1"
  cluster_ca_certificate = var.aplicacao_habilitada ? base64decode(data.aws_eks_cluster.aplicacao[0].certificate_authority[0].data) : null
  token                  = var.aplicacao_habilitada ? data.aws_eks_cluster_auth.aplicacao[0].token : "desabilitado"
}
